using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal sealed class StealthBrowser : IBrowser
{
    private readonly IBrowser _browser;
    private readonly IBrowser _owner;
    private readonly StealthCdpRelay _relay;
    private readonly Proxy? _proxy;
    private readonly HashSet<IBrowserContext> _initialContexts;
    private readonly object _closeLock = new();
    private Task? _closeTask;

    public StealthBrowser(IBrowser browser, IBrowser owner, StealthCdpRelay relay, Proxy? proxy)
    {
        _browser = browser;
        _owner = owner;
        _relay = relay;
        _proxy = proxy;
        _initialContexts = [.. browser.Contexts];
        _browser.Context += (_, context) => Context?.Invoke(this, context);
        _browser.Disconnected += OnDisconnected;
        _owner.Disconnected += OnOwnerDisconnected;
    }

    public event EventHandler<IBrowserContext>? Context;
    public event EventHandler<IBrowser>? Disconnected;
    public IBrowserType BrowserType => _browser.BrowserType;
    public IReadOnlyList<IBrowserContext> Contexts => _browser.Contexts.Where(context => !_initialContexts.Contains(context)).ToArray();
    public bool IsConnected => _browser.IsConnected;
    public string Version => _browser.Version;

    public Task<IBrowserContext> NewContextAsync(BrowserNewContextOptions? options = null)
    {
        var effective = options is null ? new BrowserNewContextOptions() : new BrowserNewContextOptions(options);
        effective.Proxy ??= _proxy;
        return _browser.NewContextAsync(effective);
    }

    public Task<IPage> NewPageAsync(BrowserNewPageOptions? options = null)
    {
        var effective = options is null ? new BrowserNewPageOptions() : new BrowserNewPageOptions(options);
        effective.Proxy ??= _proxy;
        return _browser.NewPageAsync(effective);
    }
    public Task<ICDPSession> NewBrowserCDPSessionAsync() => _browser.NewBrowserCDPSessionAsync();
    public Task<BrowserBindResult> BindAsync(string title, BrowserBindOptions? options = null) => _browser.BindAsync(title, options);
    public Task UnbindAsync() => _browser.UnbindAsync();
    public ValueTask DisposeAsync() => new(CloseAsync());

    public Task CloseAsync(BrowserCloseOptions? options = null)
    {
        lock (_closeLock)
        {
            if (_closeTask is not null)
                return _closeTask;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _closeTask = completion.Task;
            _ = CloseCoreAsync(options, completion);
            return _closeTask;
        }
    }

    private async Task CloseCoreAsync(BrowserCloseOptions? options, TaskCompletionSource completion)
    {
        try
        {
            try { await _browser.CloseAsync(options).ConfigureAwait(false); }
            finally
            {
                try { await _owner.CloseAsync(options).ConfigureAwait(false); }
                finally { await _relay.DisposeAsync().ConfigureAwait(false); }
            }
            completion.SetResult();
        }
        catch (Exception exception) { completion.SetException(exception); }
        finally
        {
            _browser.Disconnected -= OnDisconnected;
            _owner.Disconnected -= OnOwnerDisconnected;
        }
    }

    private void OnDisconnected(object? sender, IBrowser browser)
    {
        _ = CloseAfterDisconnectAsync();
        Disconnected?.Invoke(this, this);
    }

    private void OnOwnerDisconnected(object? sender, IBrowser browser) => _ = CloseAfterDisconnectAsync();

    private async Task CloseAfterDisconnectAsync()
    {
        try { await CloseAsync().ConfigureAwait(false); }
        catch { /* Explicit CloseAsync still exposes cleanup failures to its caller. */ }
    }
}
