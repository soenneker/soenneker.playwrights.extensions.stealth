using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal static class StealthRuntimeLauncher
{
    public static async Task<IBrowser> LaunchAsync(IPlaywright playwright, BrowserTypeLaunchOptions options)
    {
        // Let Playwright own process startup, browser selection, flags, and process termination.
        // Its initial connection owns no contexts; only the relayed connection creates pages.
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();

        var launchOptions = new BrowserTypeLaunchOptions(options)
        {
            Args = (options.Args ?? []).Where(static argument =>
                           !argument.StartsWith("--remote-debugging-port", StringComparison.Ordinal) &&
                           !argument.StartsWith("--remote-debugging-address", StringComparison.Ordinal))
                       .Concat([$"--remote-debugging-port={port}", "--remote-debugging-address=127.0.0.1"])
                       .ToArray()
        };
        IBrowser owner = await playwright.Chromium.LaunchAsync(launchOptions).ConfigureAwait(false);
        StealthCdpRelay? relay = null;
        try
        {
            using var handler = new HttpClientHandler { UseProxy = false };
            using var http = new HttpClient(handler);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.Timeout is > 0 ? options.Timeout.Value : 30000));
            using JsonDocument version = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/version", timeout.Token).ConfigureAwait(false));
            string endpoint = version.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!;
            var upstream = new Uri(endpoint);
            if (upstream.Scheme != "ws" || upstream.Host != "127.0.0.1" || upstream.Port != port)
                throw new InvalidOperationException("Chromium returned an unexpected debugging endpoint.");

            ICDPSession identitySession = await owner.NewBrowserCDPSessionAsync().ConfigureAwait(false);
            JsonElement? identity = await identitySession.SendAsync("Target.createBrowserContext").ConfigureAwait(false);
            string browserContextId = identity!.Value.GetProperty("browserContextId").GetString()!;
            relay = new StealthCdpRelay(upstream, browserContextId);
            IBrowser browser = await playwright.Chromium.ConnectOverCDPAsync(relay.Endpoint, new BrowserTypeConnectOverCDPOptions
            {
                Timeout = options.Timeout,
                SlowMo = options.SlowMo,
                IsLocal = true,
                ArtifactsDir = options.DownloadsPath ?? options.TracesDir
            }).ConfigureAwait(false);
            await identitySession.SendAsync("Target.disposeBrowserContext", new Dictionary<string, object> { ["browserContextId"] = browserContextId }).ConfigureAwait(false);
            await identitySession.DetachAsync().ConfigureAwait(false);
            return new StealthBrowser(browser, owner, relay, options.Proxy);
        }
        catch
        {
            try { await owner.CloseAsync().ConfigureAwait(false); }
            finally
            {
                if (relay is not null)
                    await relay.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }
}
