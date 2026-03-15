using Microsoft.Playwright;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Soenneker.Playwrights.Extensions.Stealth.Options;

namespace Soenneker.Playwrights.Extensions.Stealth;

/// <summary>
/// Extension methods for launching and configuring Playwright browser sessions with stealth-oriented defaults
/// to reduce automation detection (e.g. <c>navigator.webdriver</c>, automation-controlled flags, and fingerprint consistency).
/// </summary>
public static class PlaywrightsStealthExtension
{
    /// <summary>
    /// Launches a Chromium browser with stealth-oriented launch arguments (e.g. <c>--disable-blink-features=AutomationControlled</c>,
    /// <c>--headless=new</c> when headless, and optional stripping of detectable Playwright default args).
    /// </summary>
    /// <param name="pw">The Playwright instance.</param>
    /// <param name="options">Standard Chromium launch options; <c>Channel</c> defaults to <c>chromium</c> if not set.</param>
    /// <param name="stealthOptions">Optional settings for argument normalization and default-arg stripping; when null, default stealth options are used.</param>
    /// <returns>A task that completes with the launched <see cref="IBrowser"/>.</returns>
    [Pure]
    public static Task<IBrowser> LaunchStealthChromium(this IPlaywright pw, BrowserTypeLaunchOptions? options = null, StealthLaunchOptions? stealthOptions = null)
    {
        options ??= new BrowserTypeLaunchOptions();
        stealthOptions ??= new StealthLaunchOptions();

        options.Channel ??= "chromium";
        options.Args = StealthLaunchArgumentNormalizer.Normalize(options.Args, options.Headless == true, stealthOptions);

        if (stealthOptions.IgnoreDetectableDefaultArguments)
            options.IgnoreDefaultArgs = MergeIgnoredDefaultArguments(options.IgnoreDefaultArgs, stealthOptions.AdditionalIgnoredDefaultArguments);

        return pw.Chromium.LaunchAsync(options);
    }

    /// <summary>
    /// Creates a new browser context with stealth defaults: a generated hardware profile, shaped headers, optional document-header
    /// normalization, and an init script that reduces automation signals. Optionally uses the given proxy.
    /// </summary>
    /// <param name="browser">The browser to create the context from (typically from <see cref="LaunchStealthChromium"/>).</param>
    /// <param name="proxy">Optional proxy configuration for the new context.</param>
    /// <returns>A value task that completes with the configured <see cref="IBrowserContext"/>.</returns>
    [Pure]
    public static ValueTask<IBrowserContext> CreateStealthContext(this IBrowser browser, Proxy? proxy = null)
    {
        return CreateStealthContextCore(browser, null, new StealthContextOptions { Proxy = proxy });
    }

    /// <summary>
    /// Creates a new browser context with stealth defaults, using the provided context options and optional stealth settings.
    /// Merges profile-derived values (User-Agent, viewport, locale, timezone, etc.) with your options.
    /// </summary>
    /// <param name="browser">The browser to create the context from.</param>
    /// <param name="options">Standard Playwright context options; profile values fill in only where not specified.</param>
    /// <param name="stealthOptions">Optional stealth behavior (headers, CDP hardening, etc.); when null, default stealth options are used.</param>
    /// <returns>A value task that completes with the configured <see cref="IBrowserContext"/>.</returns>
    [Pure]
    public static ValueTask<IBrowserContext> CreateStealthContext(this IBrowser browser, BrowserNewContextOptions options, StealthContextOptions? stealthOptions = null)
    {
        return CreateStealthContextCore(browser, options, stealthOptions);
    }

    /// <summary>
    /// Applies stealth behavior to an existing browser context: configures routing for document headers (if enabled),
    /// CDP domain hardening (if enabled), and adds the stealth init script. Uses the given or a newly generated
    /// <see cref="HardwareProfile"/> for consistency.
    /// </summary>
    /// <param name="context">The existing browser context to configure.</param>
    /// <param name="stealthOptions">Optional stealth settings; when null, default stealth options are used.</param>
    /// <param name="hardwareProfile">Optional profile for headers and init script; when null, a new profile is generated and aligned with the browser version.</param>
    /// <returns>A value task that completes with the same <see cref="IBrowserContext"/> after stealth has been applied.</returns>
    [Pure]
    public static async ValueTask<IBrowserContext> ApplyStealth(this IBrowserContext context, StealthContextOptions? stealthOptions = null, HardwareProfile? hardwareProfile = null)
    {
        HardwareProfile profile = hardwareProfile ?? HardwareProfile.Generate();
        profile = profile.WithBrowserVersion(context.Browser?.Version);

        await StealthContextConfigurator.AttachAsync(context, profile, stealthOptions).NoSync();
        await StealthProtocolHardener.AttachAsync(context, stealthOptions).NoSync();
        await context.AddInitScriptAsync(StealthScriptBuilder.Build(profile)).NoSync();

        return context;
    }

    private static async ValueTask<IBrowserContext> CreateStealthContextCore(IBrowser browser, BrowserNewContextOptions? options, StealthContextOptions? stealthOptions)
    {
        HardwareProfile profile = HardwareProfile.Generate().WithBrowserVersion(browser.Version);
        BrowserNewContextOptions contextOptions = StealthContextConfigurator.BuildContextOptions(profile, options, stealthOptions);
        HardwareProfile effectiveProfile = profile.WithContextOptions(contextOptions);

        IBrowserContext context = await browser.NewContextAsync(contextOptions).NoSync();
        await context.ApplyStealth(stealthOptions, effectiveProfile).NoSync();

        return context;
    }

    private static string[] MergeIgnoredDefaultArguments(IEnumerable<string>? existingIgnoredDefaults, IEnumerable<string>? additionalIgnoredDefaults)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (existingIgnoredDefaults is not null)
        {
            foreach (string value in existingIgnoredDefaults.Where(static value => !string.IsNullOrWhiteSpace(value)))
            {
                ignored.Add(value);
            }
        }

        foreach (string value in StealthLaunchArgumentNormalizer.DetectableDefaultArgumentsToIgnore)
        {
            ignored.Add(value);
        }

        if (additionalIgnoredDefaults is not null)
        {
            foreach (string value in additionalIgnoredDefaults.Where(static value => !string.IsNullOrWhiteSpace(value)))
            {
                ignored.Add(value);
            }
        }

        return [.. ignored];
    }
}