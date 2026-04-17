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
    /// <param name="options">Standard Chromium launch options; when <c>Channel</c> is unset, <see cref="StealthLaunchOptions.Channel"/> is used (default <c>chromium</c>).</param>
    /// <param name="stealthOptions">Optional settings for argument normalization and default-arg stripping; when null, default stealth options are used.</param>
    /// <returns>A task that completes with the launched <see cref="IBrowser"/>.</returns>
    [Pure]
    public static Task<IBrowser> LaunchStealthChromium(this IPlaywright pw, BrowserTypeLaunchOptions? options = null, StealthLaunchOptions? stealthOptions = null)
    {
        options ??= new BrowserTypeLaunchOptions();
        stealthOptions ??= new StealthLaunchOptions();

        options.Args = StealthLaunchArgumentNormalizer.Normalize(options.Args, options.Headless == true, stealthOptions);

        if (stealthOptions.IgnoreDetectableDefaultArguments)
            options.IgnoreDefaultArgs = MergeIgnoredDefaultArguments(options.IgnoreDefaultArgs, stealthOptions.AdditionalIgnoredDefaultArguments);

        return LaunchStealthChromiumCore(pw, options, stealthOptions);
    }

    private static async Task<IBrowser> LaunchStealthChromiumCore(IPlaywright pw, BrowserTypeLaunchOptions options, StealthLaunchOptions stealthOptions)
    {
        if (string.IsNullOrWhiteSpace(options.Channel))
            options.Channel = string.IsNullOrWhiteSpace(stealthOptions.Channel) ? "chromium" : stealthOptions.Channel;

        return await pw.Chromium.LaunchAsync(options).NoSync();
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
        return await ApplyStealthCore(context, null, stealthOptions, hardwareProfile).NoSync();
    }

    /// <summary>
    /// Applies stealth behavior to an existing browser context using the original context options when available,
    /// allowing the generated profile and injected surfaces to stay aligned with the real context configuration.
    /// </summary>
    /// <param name="context">The existing browser context to configure.</param>
    /// <param name="contextOptions">The original options used to create the context.</param>
    /// <param name="stealthOptions">Optional stealth settings; when null, default stealth options are used.</param>
    /// <param name="hardwareProfile">Optional profile for headers and init script; when null, a new profile is generated and aligned with the browser version.</param>
    /// <returns>A value task that completes with the same <see cref="IBrowserContext"/> after stealth has been applied.</returns>
    [Pure]
    public static async ValueTask<IBrowserContext> ApplyStealth(this IBrowserContext context, BrowserNewContextOptions contextOptions,
        StealthContextOptions? stealthOptions = null, HardwareProfile? hardwareProfile = null)
    {
        return await ApplyStealthCore(context, contextOptions, stealthOptions, hardwareProfile).NoSync();
    }

    private static async ValueTask<IBrowserContext> ApplyStealthCore(IBrowserContext context, BrowserNewContextOptions? contextOptions,
        StealthContextOptions? stealthOptions, HardwareProfile? hardwareProfile)
    {
        HardwareProfile profile = hardwareProfile ?? HardwareProfile.Generate();
        profile = profile.WithBrowserVersion(context.Browser?.Version);

        if (contextOptions is not null)
        {
            profile = profile.WithContextOptions(contextOptions)
                             .WithUserAgent(contextOptions.UserAgent);
        }
        else
        {
            profile = await AlignProfileWithExistingContextAsync(context, profile).NoSync();
        }

        await StealthContextConfigurator.AttachAsync(context, profile, stealthOptions).NoSync();
        await StealthProtocolHardener.AttachAsync(context, profile, stealthOptions).NoSync();
        await context.AddInitScriptAsync(StealthScriptBuilder.Build(profile, stealthOptions)).NoSync();

        return context;
    }

    private static async ValueTask<IBrowserContext> CreateStealthContextCore(IBrowser browser, BrowserNewContextOptions? options, StealthContextOptions? stealthOptions)
    {
        HardwareProfile profile = HardwareProfile.Generate().WithBrowserVersion(browser.Version);
        BrowserNewContextOptions contextOptions = StealthContextConfigurator.BuildContextOptions(profile, options, stealthOptions);
        HardwareProfile effectiveProfile = profile.WithContextOptions(contextOptions)
                                                 .WithUserAgent(contextOptions.UserAgent);

        IBrowserContext context = await browser.NewContextAsync(contextOptions).NoSync();
        await context.ApplyStealth(stealthOptions, effectiveProfile).NoSync();

        return context;
    }

    private static async ValueTask<HardwareProfile> AlignProfileWithExistingContextAsync(IBrowserContext context, HardwareProfile profile)
    {
        IReadOnlyList<IPage> pages = context.Pages;

        if (pages.Count == 0)
            return profile;

        try
        {
            ExistingContextSnapshot? snapshot = await pages[0].EvaluateAsync<ExistingContextSnapshot>(
                """
                () => ({
                  userAgent: navigator.userAgent,
                  language: navigator.language,
                  languages: navigator.languages,
                  platform: navigator.platform,
                  maxTouchPoints: navigator.maxTouchPoints,
                  devicePixelRatio: window.devicePixelRatio,
                  screenWidth: window.screen.width,
                  screenHeight: window.screen.height,
                  timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone
                })
                """).NoSync();

            if (snapshot is null)
                return profile;

            HardwareProfile alignedProfile = profile.WithUserAgent(snapshot.UserAgent);
            string locale = string.IsNullOrWhiteSpace(snapshot.Language) ? profile.Locale : snapshot.Language;
            string[] languages = snapshot.Languages is { Count: > 0 } ? [.. snapshot.Languages.Where(static value => !string.IsNullOrWhiteSpace(value))] : profile.Languages;

            return alignedProfile with
            {
                Locale = locale,
                Languages = languages.Length == 0 ? profile.Languages : languages,
                TimeZone = HardwareProfile.NormalizeTimezoneId(snapshot.TimeZone ?? profile.TimeZone),
                Platform = string.IsNullOrWhiteSpace(snapshot.Platform) ? alignedProfile.Platform : snapshot.Platform,
                MaxTouchPoints = snapshot.MaxTouchPoints,
                IsMobile = snapshot.MaxTouchPoints > 0 || alignedProfile.IsMobile,
                ScreenW = snapshot.ScreenWidth > 0 ? snapshot.ScreenWidth : alignedProfile.ScreenW,
                ScreenH = snapshot.ScreenHeight > 0 ? snapshot.ScreenHeight : alignedProfile.ScreenH,
                DevicePixelRatio = snapshot.DevicePixelRatio > 0 ? snapshot.DevicePixelRatio : alignedProfile.DevicePixelRatio
            };
        }
        catch
        {
            return profile;
        }
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

    private sealed class ExistingContextSnapshot
    {
        public string UserAgent { get; set; } = string.Empty;
        public string? Language { get; set; }
        public List<string>? Languages { get; set; }
        public string? Platform { get; set; }
        public int MaxTouchPoints { get; set; }
        public double DevicePixelRatio { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public string? TimeZone { get; set; }
    }
}