using Microsoft.Playwright;
using Soenneker.Extensions.Task;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Soenneker.Playwrights.Extensions.Stealth.Options;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal static class StealthProtocolHardener
{
    private static readonly ConditionalWeakTable<IBrowserContext, object> _configuredContexts = [];

    public static async Task AttachAsync(IBrowserContext context, StealthContextOptions? options)
    {
        options ??= new StealthContextOptions();

        if (!options.EnableCdpDomainHardening)
            return;

        if (!_configuredContexts.TryGetValue(context, out _))
        {
            _configuredContexts.Add(context, new object());
            context.Page += (_, page) => _ = HardenPageSafeAsync(context, page, options);
        }

        IReadOnlyList<IPage> pages = context.Pages;

        for (var i = 0; i < pages.Count; i++)
        {
            await HardenPageSafeAsync(context, pages[i], options).NoSync();
        }
    }

    private static async Task HardenPageSafeAsync(IBrowserContext context, IPage page, StealthContextOptions options)
    {
        try
        {
            ICDPSession session = await context.NewCDPSessionAsync(page).NoSync();

            if (options.DisableConsoleDomain)
                await session.SendAsync("Console.disable").NoSync();

            if (options.DisableRuntimeDomain)
                await session.SendAsync("Runtime.disable").NoSync();

            await session.DetachAsync().NoSync();
        }
        catch
        {
            // Best-effort hardening. Non-Chromium targets or restricted pages can reject CDP commands.
        }
    }
}
