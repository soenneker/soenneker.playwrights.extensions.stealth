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
        HardwareProfile effectiveProfile = profile.WithContextOptions(options)
                                                 .WithUserAgent(options.UserAgent);

        options.Locale ??= effectiveProfile.Locale;
        options.TimezoneId = HardwareProfile.NormalizeTimezoneId(options.TimezoneId ?? effectiveProfile.TimeZone);
        options.ViewportSize ??= new ViewportSize
        {
            Width = effectiveProfile.ScreenW,
            Height = effectiveProfile.ScreenH
        };
        options.DeviceScaleFactor ??= (float)effectiveProfile.DevicePixelRatio;
        options.IsMobile ??= effectiveProfile.IsMobile;
        options.HasTouch ??= effectiveProfile.MaxTouchPoints > 0;
        options.Proxy ??= stealthOptions.Proxy;

        if (stealthOptions.RandomizeGeolocation)
            options.Permissions = MergePermissions(options.Permissions, ["geolocation"]);

        if (stealthOptions.AlignColorScheme)
            options.ColorScheme ??= effectiveProfile.PrefersDarkMode ? ColorScheme.Dark : ColorScheme.Light;

        options.ExtraHTTPHeaders = MergeHeaders(options.ExtraHTTPHeaders, StealthHeaderBuilder.BuildContextHeaders(effectiveProfile, stealthOptions));

        return options;
    }

    public static async Task AttachAsync(IBrowserContext context, HardwareProfile profile, StealthContextOptions? stealthOptions = null)
    {
        stealthOptions ??= new StealthContextOptions();

        if (stealthOptions.RandomizeGeolocation)
        {
            await context.SetGeolocationAsync(new Geolocation
            {
                Latitude = (float)profile.Latitude,
                Longitude = (float)profile.Longitude,
                Accuracy = 18
            }).NoSync();

            await context.GrantPermissionsAsync(["geolocation"]).NoSync();
        }

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
            Dictionary<string, string> headers = StealthHeaderBuilder.BuildDocumentHeaders(profile, requestHeaders, route.Request.Url, stealthOptions);

            await route.ContinueAsync(new RouteContinueOptions
            {
                Headers = headers
            }).NoSync();
        }).NoSync();
    }

    private static string[] MergePermissions(IEnumerable<string>? existingPermissions, IReadOnlyCollection<string> generatedPermissions)
    {
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (existingPermissions is not null)
        {
            foreach (string permission in existingPermissions)
            {
                if (!string.IsNullOrWhiteSpace(permission))
                    merged.Add(permission);
            }
        }

        foreach (string permission in generatedPermissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
                merged.Add(permission);
        }

        return [.. merged];
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
