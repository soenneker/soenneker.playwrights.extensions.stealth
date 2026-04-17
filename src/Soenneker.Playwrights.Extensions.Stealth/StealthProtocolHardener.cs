using Microsoft.Playwright;
using Soenneker.Extensions.Task;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;
using Soenneker.Playwrights.Extensions.Stealth.Options;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal static class StealthProtocolHardener
{
    private static readonly ConditionalWeakTable<IBrowserContext, ContextCdpState> _configuredContexts = [];

    public static async Task AttachAsync(IBrowserContext context, HardwareProfile profile, StealthContextOptions? options)
    {
        options ??= new StealthContextOptions();

        if (!_configuredContexts.TryGetValue(context, out ContextCdpState? state))
        {
            state = new ContextCdpState
            {
                Profile = profile,
                Options = options
            };
            _configuredContexts.Add(context, state);
            context.Page += (_, page) => _ = ConfigurePageSafe(context, page);
        }
        else
        {
            state.Profile = profile;
            state.Options = options;
        }

        IReadOnlyList<IPage> pages = context.Pages;

        for (var i = 0; i < pages.Count; i++)
        {
            await ConfigurePageSafe(context, pages[i]).NoSync();
        }
    }

    private static async ValueTask ConfigurePageSafe(IBrowserContext context, IPage page)
    {
        try
        {
            if (!_configuredContexts.TryGetValue(context, out ContextCdpState? state))
                return;

            ICDPSession session = await context.NewCDPSessionAsync(page).NoSync();

            await session.SendAsync("Emulation.setUserAgentOverride", StealthHeaderBuilder.BuildUserAgentOverrideParameters(state.Profile)).NoSync();

            if (state.Options.EnableCdpDomainHardening && state.Options.DisableConsoleDomain)
                await session.SendAsync("Console.disable").NoSync();

            if (state.Options.EnableCdpDomainHardening && state.Options.DisableRuntimeDomain)
                await session.SendAsync("Runtime.disable").NoSync();

            await session.DetachAsync().NoSync();
        }
        catch
        {
            // Best-effort hardening. Non-Chromium targets or restricted pages can reject CDP commands.
        }
    }

    private sealed class ContextCdpState
    {
        public required HardwareProfile Profile { get; set; }
        public required StealthContextOptions Options { get; set; }
    }
}
