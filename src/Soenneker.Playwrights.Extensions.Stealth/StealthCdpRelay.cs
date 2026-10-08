using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Semaphores;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal sealed class StealthCdpRelay : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Uri _upstream;
    private readonly string _browserContextId;
    private readonly string _path = "/" + Guid.NewGuid().ToString("N");
    private readonly AsyncSemaphore _upstreamWriter = new(1, 1);
    private readonly ConcurrentDictionary<long, string?> _runtimeRequests = new();
    private readonly ConcurrentDictionary<long, JsonObject> _pendingReplies = new();
    private readonly AsyncSemaphore _requestLock = new(1, 1);
    private readonly Dictionary<string, Queue<JsonObject>> _deferredRequests = new();
    private long _nextId;

    public StealthCdpRelay(Uri upstream, string browserContextId)
    {
        _upstream = upstream;
        _browserContextId = browserContextId;
        _listener.Start();
        Endpoint = $"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}{_path}";
        Completion = RunAsync();
    }

    public string Endpoint { get; }
    public Task Completion { get; }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try { await Completion.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (IOException) { }
    }

    private async Task RunAsync()
    {
        try { await RunConnectionAsync().ConfigureAwait(false); }
        finally { _listener.Stop(); }
    }

    private async Task RunConnectionAsync()
    {
        CancellationToken cancellationToken = _stop.Token;
        using var upstream = new ClientWebSocket();
        upstream.Options.Proxy = null;
        await upstream.ConnectAsync(_upstream, cancellationToken).ConfigureAwait(false);
        // Verify ownership after releasing the temporary port reservation. A port collision
        // must fail rather than attach to a different local Chromium process.
        await SendAsync(upstream, new JsonObject { ["id"] = 0, ["method"] = "Target.getBrowserContexts" }, cancellationToken).ConfigureAwait(false);
        JsonObject? identity = await ReadAsync(upstream, cancellationToken).ConfigureAwait(false);
        bool owned = false;
        if (identity?["result"]?["browserContextIds"] is JsonArray contexts)
        {
            foreach (JsonNode? context in contexts)
                owned |= context?.GetValue<string>() == _browserContextId;
        }
        if (!owned)
            throw new IOException("The Chromium debugging endpoint does not belong to the launched browser.");

        while (!cancellationToken.IsCancellationRequested)
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            client.NoDelay = true;
            using NetworkStream stream = client.GetStream();
            using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            WebSocket? socket;
            try { socket = await AcceptAsync(stream, handshakeTimeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { continue; }
            if (socket is null)
                continue;

            using (socket)
            {
                _listener.Stop();
                using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task requests = ForwardRequestsAsync(socket, upstream, connection.Token);
                Task responses = ForwardResponsesAsync(upstream, socket, connection.Token);
                try { await await Task.WhenAny(requests, responses).ConfigureAwait(false); }
                finally
                {
                    await connection.CancelAsync().ConfigureAwait(false);
                    socket.Abort();
                    upstream.Abort();
                    try { await Task.WhenAll(requests, responses).ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                    catch (WebSocketException) { }
                }
            }
            return;
        }
    }

    private async Task<WebSocket?> AcceptAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new StringBuilder();
        var buffer = new byte[1];
        while (header.Length < 8192)
        {
            if (await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) == 0)
                return null;
            header.Append((char)buffer[0]);
            if (header.Length >= 4 && header.ToString(header.Length - 4, 4) == "\r\n\r\n")
                break;
        }

        string headers = header.ToString();
        if (!headers.EndsWith("\r\n\r\n", StringComparison.Ordinal))
            return null;
        string[] lines = headers.Split("\r\n", StringSplitOptions.None);
        if (lines[0] != $"GET {_path} HTTP/1.1")
            return null;

        string? key = null;
        bool upgrade = false;
        foreach (string line in lines)
        {
            // This endpoint is for the local Playwright driver, never browser-origin requests.
            if (line.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))
                return null;
            if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                key = line[18..].Trim();
            if (line.Equals("Upgrade: websocket", StringComparison.OrdinalIgnoreCase))
                upgrade = true;
        }
        if (key is null || !upgrade)
            return null;

        string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        return WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(30));
    }

    private async Task ForwardRequestsAsync(WebSocket client, WebSocket upstream, CancellationToken cancellationToken)
    {
        while (await ReadAsync(client, cancellationToken).ConfigureAwait(false) is { } message)
            await ForwardRequestAsync(upstream, message, cancellationToken).ConfigureAwait(false);
    }

    private async Task ForwardRequestAsync(WebSocket upstream, JsonObject message, CancellationToken cancellationToken)
    {
        using SemaphoreLease lease = await _requestLock.Acquire(cancellationToken).ConfigureAwait(false);
        await ForwardRequestCoreAsync(upstream, message, cancellationToken).ConfigureAwait(false);
    }

    private async Task ForwardRequestCoreAsync(WebSocket upstream, JsonObject message, CancellationToken cancellationToken)
    {
        string? sessionId = message["sessionId"]?.GetValue<string>();
        if (_deferredRequests.TryGetValue(sessionId ?? "", out Queue<JsonObject>? deferred))
        {
            deferred.Enqueue(message);
            return;
        }
        if (message["method"]?.GetValue<string>() == "Runtime.enable")
        {
            _runtimeRequests[message["id"]!.GetValue<long>()] = sessionId;
            _deferredRequests[sessionId ?? ""] = new Queue<JsonObject>();
        }
        await SendUpstreamAsync(upstream, message, cancellationToken).ConfigureAwait(false);
    }

    private async Task ForwardResponsesAsync(WebSocket upstream, WebSocket client, CancellationToken cancellationToken)
    {
        while (await ReadAsync(upstream, cancellationToken).ConfigureAwait(false) is { } message)
        {
            if (message["id"] is { } idNode)
            {
                long id = idNode.GetValue<long>();
                if (_pendingReplies.TryRemove(id, out JsonObject? reply))
                {
                    // Do not acknowledge Runtime.enable until stack capture has been configured.
                    if (message["error"] is { } error)
                    {
                        reply.Remove("result");
                        reply["error"] = error.DeepClone();
                        await SendAsync(client, reply, cancellationToken).ConfigureAwait(false);
                        throw new IOException($"Chromium rejected Runtime stack hardening: {error}");
                    }
                    await ReleaseSessionAsync(upstream, client, reply, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (_runtimeRequests.TryRemove(id, out string? sessionId))
                {
                    if (message["error"] is not null)
                    {
                        // A target can close during initialization. Preserve the normal CDP error
                        // without taking down unrelated pages; Runtime was not enabled here.
                        await ReleaseSessionAsync(upstream, client, message, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    long commandId = Interlocked.Decrement(ref _nextId);
                    _pendingReplies[commandId] = message;
                    var command = new JsonObject
                    {
                        ["id"] = commandId,
                        ["method"] = "Runtime.setMaxCallStackSizeToCapture",
                        ["params"] = new JsonObject { ["size"] = 0 }
                    };
                    if (sessionId is not null)
                        command["sessionId"] = sessionId;
                    await SendUpstreamAsync(upstream, command, cancellationToken).ConfigureAwait(false);
                    continue;
                }
            }
            await SendAsync(client, message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReleaseSessionAsync(WebSocket upstream, WebSocket client, JsonObject reply, CancellationToken cancellationToken)
    {
        using SemaphoreLease lease = await _requestLock.Acquire(cancellationToken).ConfigureAwait(false);
        _deferredRequests.Remove(reply["sessionId"]?.GetValue<string>() ?? "", out Queue<JsonObject>? deferred);
        await SendAsync(client, reply, cancellationToken).ConfigureAwait(false);
        if (deferred is not null)
        {
            foreach (JsonObject request in deferred)
                await ForwardRequestCoreAsync(upstream, request, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendUpstreamAsync(WebSocket upstream, JsonObject message, CancellationToken cancellationToken)
    {
        using SemaphoreLease lease = await _upstreamWriter.Acquire(cancellationToken).ConfigureAwait(false);
        await SendAsync(upstream, message, cancellationToken).ConfigureAwait(false);
    }

    private static Task SendAsync(WebSocket socket, JsonObject message, CancellationToken cancellationToken)
    {
        return socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message.ToJsonString())), WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<JsonObject?> ReadAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16384];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > 64 * 1024 * 1024)
                throw new IOException("Invalid or oversized CDP message.");
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return JsonNode.Parse(message.GetBuffer().AsSpan(0, (int)message.Length))!.AsObject();
    }
}
