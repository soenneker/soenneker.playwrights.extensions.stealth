using Microsoft.Playwright;
using Soenneker.Extensions.Task;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Soenneker.Playwrights.Extensions.Stealth.Options;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal static class StealthContextConfigurator
{
    public static BrowserNewContextOptions BuildContextOptions(HardwareProfile profile, BrowserNewContextOptions? options = null, StealthContextOptions? stealthOptions = null)
    {
        options ??= new BrowserNewContextOptions();
        stealthOptions ??= new StealthContextOptions();

        options.UserAgent ??= StealthHeaderBuilder.BuildUserAgent(profile);
        options.Locale ??= profile.Locale;
        options.TimezoneId = HardwareProfile.NormalizeTimezoneId(options.TimezoneId ?? profile.TimeZone);
        options.ViewportSize ??= new ViewportSize
        {
            Width = profile.ScreenW,
            Height = profile.ScreenH
        };
        options.DeviceScaleFactor ??= (float)profile.DevicePixelRatio;
        options.Proxy ??= stealthOptions.Proxy;
        if (stealthOptions.AlignColorScheme)
            options.ColorScheme ??= profile.PrefersDarkMode ? ColorScheme.Dark : ColorScheme.Light;

        options.ExtraHTTPHeaders = MergeHeaders(options.ExtraHTTPHeaders, StealthHeaderBuilder.BuildContextHeaders(profile, stealthOptions));

        return options;
    }

    public static async Task AttachAsync(IBrowserContext context, HardwareProfile profile, StealthContextOptions? stealthOptions = null)
    {
        stealthOptions ??= new StealthContextOptions();

        if (!stealthOptions.NormalizeDocumentHeaders)
            return;

        await context.RouteAsync("**/*", async route =>
        {
            if (!string.Equals(route.Request.ResourceType, "document", StringComparison.OrdinalIgnoreCase))
            {
                await route.ContinueAsync().NoSync();
                return;
            }

            IReadOnlyDictionary<string, string> requestHeaders = await route.Request.AllHeadersAsync().NoSync();
            Dictionary<string, string> headers = StealthHeaderBuilder.BuildDocumentHeaders(profile, requestHeaders, route.Request.Url);

            await route.ContinueAsync(new RouteContinueOptions
            {
                Headers = headers
            }).NoSync();
        }).NoSync();
    }

    private static Dictionary<string, string> MergeHeaders(IEnumerable<KeyValuePair<string, string>>? existingHeaders, IReadOnlyDictionary<string, string> generatedHeaders)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (existingHeaders is not null)
        {
            foreach ((string key, string value) in existingHeaders)
            {
                merged[key] = value;
            }
        }

        foreach ((string key, string value) in generatedHeaders)
        {
            merged[key] = value;
        }

        return merged;
    }
}
