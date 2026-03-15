using Microsoft.Playwright;
using Soenneker.Utils.Random;
using System;
using System.Globalization;
using System.Linq;

namespace Soenneker.Playwrights.Extensions.Stealth;

/// <summary>
/// A coherent hardware, browser, and location profile for a stealth Playwright context.
/// Values reflect common Chrome-on-Windows environments and stay consistent across
/// launch args, HTTP headers, init-script JS surfaces, and routed document requests.
/// </summary>
/// <param name="Cores">CPU core count exposed as <c>navigator.hardwareConcurrency</c>.</param>
/// <param name="MemoryGb">Device memory in GB for <c>navigator.deviceMemory</c>.</param>
/// <param name="Platform">Platform string (e.g. Win32) for <c>navigator.platform</c>.</param>
/// <param name="OsPlatform">OS name for Client Hints and UA.</param>
/// <param name="OsPlatformVersion">OS version string.</param>
/// <param name="Architecture">CPU architecture (e.g. x86).</param>
/// <param name="Bitness">Pointer size / bitness (e.g. 64).</param>
/// <param name="ScreenW">Screen width in CSS pixels.</param>
/// <param name="ScreenH">Screen height in CSS pixels.</param>
/// <param name="DevicePixelRatio">Device pixel ratio for screen and viewport.</param>
/// <param name="MaxTouchPoints">Maximum touch points.</param>
/// <param name="ChromeVersion">Full Chrome version string for User-Agent and init script.</param>
/// <param name="ChromeMajorVersion">Chrome major version number.</param>
/// <param name="Seed">Random seed used to generate this profile (for reproducibility).</param>
/// <param name="Latitude">Geolocation latitude.</param>
/// <param name="Longitude">Geolocation longitude.</param>
/// <param name="TimeZone">IANA timezone ID.</param>
/// <param name="Locale">Locale string (e.g. en-US).</param>
/// <param name="Languages">Ordered list of language codes for Accept-Language and <c>navigator.languages</c>.</param>
/// <param name="PrefersDarkMode">Whether to use dark color scheme.</param>
/// <param name="BrowserVendor">Browser vendor string (e.g. Google Inc.).</param>
/// <param name="WebGlVendor">WebGL vendor string for spoofing.</param>
/// <param name="WebGlRenderer">WebGL renderer string for spoofing.</param>
/// <param name="ColorDepth">Screen color depth in bits.</param>
/// <param name="PixelDepth">Pixel depth in bits.</param>
public sealed record HardwareProfile(
    int Cores,
    int MemoryGb,
    string Platform,
    string OsPlatform,
    string OsPlatformVersion,
    string Architecture,
    string Bitness,
    int ScreenW,
    int ScreenH,
    double DevicePixelRatio,
    int MaxTouchPoints,
    string ChromeVersion,
    int ChromeMajorVersion,
    int Seed,
    double Latitude,
    double Longitude,
    string TimeZone,
    string Locale,
    string[] Languages,
    bool PrefersDarkMode,
    string BrowserVendor,
    string WebGlVendor,
    string WebGlRenderer,
    int ColorDepth,
    int PixelDepth)
{
    /// <summary>
    /// Generates a new random hardware profile with coherent Windows/Chrome values
    /// (cores, memory, viewport, DPR, Chrome version, timezone, locale, WebGL, geolocation, etc.).
    /// </summary>
    /// <returns>A new <see cref="HardwareProfile"/> instance.</returns>
    public static HardwareProfile Generate()
    {
        int seed = RandomUtil.Next(int.MinValue, int.MaxValue);
        var rnd = new Random(seed);

        int[] cores = [4, 6, 8, 12, 16];
        int[] memories = [8, 16, 32];
        double[] dprs = [1, 1.25, 1.5, 2];

        (int width, int height)[] screens =
        [
            (1366, 768),
            (1536, 864),
            (1920, 1080),
            (2560, 1440)
        ];

        (double lat, double lng)[] regions =
        [
            (40.7128, -74.0060),
            (41.8781, -87.6298),
            (34.0522, -118.2437)
        ];

        (string vendor, string renderer)[] webGlProfiles =
        [
            ("Intel Inc.", "Intel Iris OpenGL Engine"),
            ("Google Inc. (Intel)", "ANGLE (Intel, Intel(R) UHD Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)"),
            ("Google Inc. (NVIDIA)", "ANGLE (NVIDIA, NVIDIA GeForce GTX 1650 Direct3D11 vs_5_0 ps_5_0, D3D11)"),
            ("Google Inc. (AMD)", "ANGLE (AMD, Radeon RX 580 Series Direct3D11 vs_5_0 ps_5_0, D3D11)")
        ];

        const int chromeMajor = 144;
        string chromeFull = $"{chromeMajor}.0.{rnd.Next(7000, 7999)}.{rnd.Next(50, 160)}";
        string locale = GetSystemLocale();
        string[] languages = BuildLanguages(locale);
        string timeZone = NormalizeTimezoneId(TimeZoneInfo.Local.Id);

        var screen = screens[rnd.Next(screens.Length)];
        var region = regions[rnd.Next(regions.Length)];
        var webGl = webGlProfiles[rnd.Next(webGlProfiles.Length)];

        double latJitter = (rnd.NextDouble() - 0.5) * 0.18;
        double lngJitter = (rnd.NextDouble() - 0.5) * 0.18;

        return new HardwareProfile(Cores: cores[rnd.Next(cores.Length)], MemoryGb: memories[rnd.Next(memories.Length)], Platform: "Win32",
            OsPlatform: "Windows", OsPlatformVersion: "19.0.0", Architecture: "x86", Bitness: "64", ScreenW: screen.width, ScreenH: screen.height,
            DevicePixelRatio: dprs[rnd.Next(dprs.Length)], MaxTouchPoints: 0, ChromeVersion: chromeFull, ChromeMajorVersion: chromeMajor, Seed: seed,
            Latitude: Math.Round(region.lat + latJitter, 5), Longitude: Math.Round(region.lng + lngJitter, 5), TimeZone: timeZone, Locale: locale,
            Languages: languages, PrefersDarkMode: rnd.NextDouble() < 0.7, BrowserVendor: "Google Inc.", WebGlVendor: webGl.vendor,
            WebGlRenderer: webGl.renderer, ColorDepth: 24, PixelDepth: 24);
    }

    /// <summary>
    /// Returns a copy of this profile with locale, timezone, viewport, and device scale factor
    /// overridden from the given context options where they are set.
    /// </summary>
    /// <param name="options">Playwright context options; only non-null locale, timezone, viewport, and device scale factor are applied.</param>
    /// <returns>A new <see cref="HardwareProfile"/> with the overrides applied, or this instance if <paramref name="options"/> is null.</returns>
    public HardwareProfile WithContextOptions(BrowserNewContextOptions? options)
    {
        if (options is null)
            return this;

        string locale = string.IsNullOrWhiteSpace(options.Locale) ? Locale : options.Locale;
        string[] languages = string.IsNullOrWhiteSpace(options.Locale) ? Languages : BuildLanguages(options.Locale);

        return this with
        {
            Locale = locale,
            Languages = languages,
            TimeZone = string.IsNullOrWhiteSpace(options.TimezoneId) ? TimeZone : NormalizeTimezoneId(options.TimezoneId),
            ScreenW = options.ViewportSize?.Width ?? ScreenW,
            ScreenH = options.ViewportSize?.Height ?? ScreenH,
            DevicePixelRatio = options.DeviceScaleFactor is null ? DevicePixelRatio : options.DeviceScaleFactor.Value
        };
    }

    /// <summary>
    /// Returns a copy of this profile with Chrome version and major version set from the given
    /// browser version string (e.g. from <see cref="IBrowser.Version"/>). If parsing fails, returns this instance unchanged.
    /// </summary>
    /// <param name="browserVersion">The browser version string, e.g. <c>chromium/120.0.6099.28</c>.</param>
    /// <returns>A new <see cref="HardwareProfile"/> with updated Chrome version fields, or this instance if <paramref name="browserVersion"/> is null or unparseable.</returns>
    public HardwareProfile WithBrowserVersion(string? browserVersion)
    {
        if (!TryParseChromiumVersion(browserVersion, out string parsedVersion, out int parsedMajor))
            return this;

        return this with
        {
            ChromeVersion = parsedVersion,
            ChromeMajorVersion = parsedMajor
        };
    }

    internal static string NormalizeTimezoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return "UTC";

        if (timeZoneId.Contains('/'))
            return timeZoneId;

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out string? ianaTimeZoneId) && !string.IsNullOrWhiteSpace(ianaTimeZoneId))
            return ianaTimeZoneId;

        return timeZoneId;
    }

    private static string GetSystemLocale()
    {
        string locale = CultureInfo.CurrentUICulture.Name;

        if (!string.IsNullOrWhiteSpace(locale))
            return locale;

        locale = CultureInfo.CurrentCulture.Name;

        return string.IsNullOrWhiteSpace(locale) ? "en-US" : locale;
    }

    private static string[] BuildLanguages(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return ["en-US", "en"];

        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);

            if (culture.Parent is { Name.Length: > 0 })
                return [culture.Name, culture.Parent.Name];

            return [culture.Name];
        }
        catch (CultureNotFoundException)
        {
            int separatorIndex = locale.IndexOf('-');
            return separatorIndex > 0 ? [locale, locale[..separatorIndex]] : [locale];
        }
    }

    private static bool TryParseChromiumVersion(string? browserVersion, out string parsedVersion, out int parsedMajor)
    {
        parsedVersion = string.Empty;
        parsedMajor = 0;

        if (string.IsNullOrWhiteSpace(browserVersion))
            return false;

        string candidate = browserVersion;
        int slashIndex = candidate.LastIndexOf('/');

        if (slashIndex >= 0 && slashIndex < candidate.Length - 1)
            candidate = candidate[(slashIndex + 1)..];

        char[] versionChars = candidate.TakeWhile(static character => char.IsDigit(character) || character == '.')
                                       .ToArray();

        if (versionChars.Length == 0)
            return false;

        string version = new(versionChars);

        if (string.IsNullOrWhiteSpace(version))
            return false;

        string[] segments = version.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || !int.TryParse(segments[0], out int major))
            return false;

        parsedVersion = version;
        parsedMajor = major;
        return true;
    }
}