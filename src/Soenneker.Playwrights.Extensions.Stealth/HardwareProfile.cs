using Microsoft.Playwright;
using Soenneker.Utils.Random;
using System;
using System.Globalization;
using System.Linq;
using Soenneker.Extensions.String;

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
    public string DeviceModel { get; init; } = string.Empty;
    public bool IsMobile { get; init; }
    public string? UserAgentOverride { get; init; }

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
            WebGlRenderer: webGl.renderer, ColorDepth: 24, PixelDepth: 24)
        {
            IsMobile = false
        };
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

        string locale = options.Locale.IsNullOrWhiteSpace() ? Locale : options.Locale;
        string[] languages = string.IsNullOrWhiteSpace(options.Locale) ? Languages : BuildLanguages(options.Locale);

        return this with
        {
            Locale = locale,
            Languages = languages,
            TimeZone = string.IsNullOrWhiteSpace(options.TimezoneId) ? TimeZone : NormalizeTimezoneId(options.TimezoneId),
            ScreenW = options.ViewportSize?.Width ?? ScreenW,
            ScreenH = options.ViewportSize?.Height ?? ScreenH,
            DevicePixelRatio = options.DeviceScaleFactor is null ? DevicePixelRatio : options.DeviceScaleFactor.Value,
            IsMobile = options.IsMobile ?? IsMobile,
            MaxTouchPoints = ResolveMaxTouchPoints(options, IsMobile, MaxTouchPoints)
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

    /// <summary>
    /// Returns a copy of this profile with a caller-supplied User-Agent override. When the User-Agent
    /// contains a Chromium version token, the profile's Chrome version fields are updated so Client Hints
    /// and JS-exposed surfaces stay aligned with the override.
    /// </summary>
    /// <param name="userAgent">The User-Agent string to apply.</param>
    /// <returns>A new <see cref="HardwareProfile"/> aligned with the supplied User-Agent.</returns>
    public HardwareProfile WithUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return this with { UserAgentOverride = null };

        var updatedProfile = this with { UserAgentOverride = userAgent };

        if (TryParseUserAgentEnvironment(userAgent, out UserAgentEnvironment environment))
        {
            updatedProfile = updatedProfile with
            {
                Platform = environment.Platform,
                OsPlatform = environment.OsPlatform,
                OsPlatformVersion = environment.OsPlatformVersion,
                Architecture = environment.Architecture,
                Bitness = environment.Bitness,
                ScreenW = environment.ScreenW,
                ScreenH = environment.ScreenH,
                DevicePixelRatio = environment.DevicePixelRatio,
                DeviceModel = environment.DeviceModel,
                IsMobile = environment.IsMobile,
                MaxTouchPoints = environment.MaxTouchPoints
            };
        }

        if (!TryParseUserAgentChromiumVersion(userAgent, out string parsedVersion, out int parsedMajor))
            return updatedProfile;

        return updatedProfile with
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

    private static int ResolveMaxTouchPoints(BrowserNewContextOptions options, bool isMobile, int currentMaxTouchPoints)
    {
        if (options.HasTouch == true)
            return Math.Max(currentMaxTouchPoints, 5);

        if (options.HasTouch == false)
            return 0;

        return options.IsMobile == true ? Math.Max(currentMaxTouchPoints, 5) : currentMaxTouchPoints;
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

    private static bool TryParseUserAgentChromiumVersion(string userAgent, out string parsedVersion, out int parsedMajor)
    {
        parsedVersion = string.Empty;
        parsedMajor = 0;

        return TryExtractVersionCandidate(userAgent, "Chrome/", out string chromeCandidate) && TryParseChromiumVersion(chromeCandidate, out parsedVersion, out parsedMajor) ||
               TryExtractVersionCandidate(userAgent, "Chromium/", out string chromiumCandidate) && TryParseChromiumVersion(chromiumCandidate, out parsedVersion, out parsedMajor) ||
               TryExtractVersionCandidate(userAgent, "CriOS/", out string criosCandidate) && TryParseChromiumVersion(criosCandidate, out parsedVersion, out parsedMajor);
    }

    private static bool TryExtractVersionCandidate(string value, string token, out string candidate)
    {
        candidate = string.Empty;

        int tokenIndex = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);

        if (tokenIndex < 0)
            return false;

        int versionStartIndex = tokenIndex + token.Length;

        if (versionStartIndex >= value.Length)
            return false;

        char[] versionChars = value[versionStartIndex..]
                              .TakeWhile(static character => char.IsDigit(character) || character is '.' or '_')
                              .ToArray();

        if (versionChars.Length == 0)
            return false;

        candidate = new string(versionChars);
        return !string.IsNullOrWhiteSpace(candidate);
    }

    private static bool TryParseUserAgentEnvironment(string userAgent, out UserAgentEnvironment environment)
    {
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            bool isMobile = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase);
            environment = new UserAgentEnvironment(
                Platform: "Linux armv8l",
                OsPlatform: "Android",
                OsPlatformVersion: ParseOsVersion(userAgent, "Android ", '.'),
                Architecture: "arm",
                Bitness: "64",
                DeviceModel: ParseAndroidDeviceModel(userAgent),
                IsMobile: isMobile,
                ScreenW: isMobile ? 412 : 800,
                ScreenH: isMobile ? 915 : 1280,
                DevicePixelRatio: isMobile ? 2.625 : 2,
                MaxTouchPoints: 5);
            return true;
        }

        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
        {
            environment = new UserAgentEnvironment(
                Platform: "iPhone",
                OsPlatform: "iOS",
                OsPlatformVersion: ParseOsVersion(userAgent, "iPhone OS ", '_'),
                Architecture: "arm",
                Bitness: "64",
                DeviceModel: "iPhone",
                IsMobile: true,
                ScreenW: 390,
                ScreenH: 844,
                DevicePixelRatio: 3,
                MaxTouchPoints: 5);
            return true;
        }

        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
        {
            environment = new UserAgentEnvironment(
                Platform: "iPad",
                OsPlatform: "iOS",
                OsPlatformVersion: ParseOsVersion(userAgent, "CPU OS ", '_'),
                Architecture: "arm",
                Bitness: "64",
                DeviceModel: "iPad",
                IsMobile: true,
                ScreenW: 820,
                ScreenH: 1180,
                DevicePixelRatio: 2,
                MaxTouchPoints: 5);
            return true;
        }

        if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
        {
            environment = new UserAgentEnvironment(
                Platform: "MacIntel",
                OsPlatform: "macOS",
                OsPlatformVersion: ParseOsVersion(userAgent, "Mac OS X ", '_'),
                Architecture: "x86",
                Bitness: "64",
                DeviceModel: string.Empty,
                IsMobile: false,
                ScreenW: 1512,
                ScreenH: 982,
                DevicePixelRatio: 2,
                MaxTouchPoints: 0);
            return true;
        }

        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            environment = new UserAgentEnvironment(
                Platform: "Win32",
                OsPlatform: "Windows",
                OsPlatformVersion: ParseOsVersion(userAgent, "Windows NT ", '.'),
                Architecture: "x86",
                Bitness: "64",
                DeviceModel: string.Empty,
                IsMobile: false,
                ScreenW: 1920,
                ScreenH: 1080,
                DevicePixelRatio: 1,
                MaxTouchPoints: 0);
            return true;
        }

        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("X11", StringComparison.OrdinalIgnoreCase))
        {
            environment = new UserAgentEnvironment(
                Platform: "Linux x86_64",
                OsPlatform: "Linux",
                OsPlatformVersion: "0.0.0",
                Architecture: "x86",
                Bitness: "64",
                DeviceModel: string.Empty,
                IsMobile: false,
                ScreenW: 1920,
                ScreenH: 1080,
                DevicePixelRatio: 1,
                MaxTouchPoints: 0);
            return true;
        }

        environment = default;
        return false;
    }

    private static string ParseOsVersion(string value, string token, char separator)
    {
        if (!TryExtractVersionCandidate(value, token, out string candidate))
            return "0.0.0";

        string normalized = candidate.Replace(separator, '.');
        string[] segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return "0.0.0";

        return segments.Length switch
        {
            1 => $"{segments[0]}.0.0",
            2 => $"{segments[0]}.{segments[1]}.0",
            _ => $"{segments[0]}.{segments[1]}.{segments[2]}"
        };
    }

    private static string ParseAndroidDeviceModel(string userAgent)
    {
        int androidIndex = userAgent.IndexOf("Android ", StringComparison.OrdinalIgnoreCase);

        if (androidIndex < 0)
            return string.Empty;

        int afterAndroidIndex = userAgent.IndexOf(';', androidIndex);

        if (afterAndroidIndex < 0 || afterAndroidIndex >= userAgent.Length - 1)
            return string.Empty;

        string candidate = userAgent[(afterAndroidIndex + 1)..];
        int closingParenIndex = candidate.IndexOf(')');

        if (closingParenIndex >= 0)
            candidate = candidate[..closingParenIndex];

        string[] segments = candidate.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];

            if (segment.StartsWith("Build/", StringComparison.OrdinalIgnoreCase))
                continue;

            return segment;
        }

        return string.Empty;
    }

    private readonly record struct UserAgentEnvironment(string Platform, string OsPlatform, string OsPlatformVersion, string Architecture, string Bitness,
        string DeviceModel, bool IsMobile, int ScreenW, int ScreenH, double DevicePixelRatio, int MaxTouchPoints);
}