using Soenneker.Utils.Random;
using System;

namespace Soenneker.Playwrights.Extensions.Stealth;

/// <summary>
/// Represents a coherent hardware, browser, and location profile for a Playwright context.
/// Values are chosen to reflect common real-world Chrome-on-Windows environments
/// and remain internally consistent across headers, JS surface, and network signals.
/// </summary>
public sealed record HardwareProfile(
    int Cores,
    int MemoryGb,
    string Platform,                 // navigator.platform (e.g. "Win32")
    string OsPlatform,               // sec-ch-ua-platform (e.g. "Windows")
    string OsPlatformVersion,        // sec-ch-ua-platform-version (e.g. "19.0.0")
    int ScreenW,
    int ScreenH,
    string ChromeVersion,            // full version: 144.0.x.y
    int ChromeMajorVersion,           // 144
    int Seed,
    float Latitude,
    float Longitude,
    string TimeZone,
    string Locale,                   // e.g. en-US
    bool PrefersDarkMode
)
{
    /// <summary>
    /// Generates a new <see cref="HardwareProfile"/> with realistic, internally
    /// consistent values suitable for stealth browser contexts.
    /// </summary>
    public static HardwareProfile Generate()
    {
        // Stable per-context seed
        int seed = RandomUtil.Next(int.MinValue, int.MaxValue);
        var rnd = new Random(seed);

        // Typical desktop distributions
        int[] cores = [4, 6, 8, 12, 16];
        int[] memories = [8, 16, 32];
        (int w, int h)[] screens =
        [
            (1920, 1080),
            (2560, 1440),
            (3840, 2160)
        ];

        // Chrome 144 (keep major + full version aligned)
        const int chromeMajor = 144;
        var chromeFull = $"{chromeMajor}.0.{rnd.Next(7000, 7999)}.{rnd.Next(50, 150)}";

        // NYC-ish bounding box (dense, common ASN region)
        double lat = 40.55 + rnd.NextDouble() * (40.96 - 40.55);
        double lng = -74.25 + rnd.NextDouble() * (-73.70 + 74.25);

        return new HardwareProfile(
            Cores: cores[rnd.Next(cores.Length)],
            MemoryGb: memories[rnd.Next(memories.Length)],

            // JS + CH consistency
            Platform: "Win32",
            OsPlatform: "Windows",
            OsPlatformVersion: "19.0.0", // Windows 10/11 reported value

            ScreenW: screens[rnd.Next(screens.Length)].w,
            ScreenH: screens[rnd.Next(screens.Length)].h,

            ChromeVersion: chromeFull,
            ChromeMajorVersion: chromeMajor,

            Seed: seed,

            Latitude: (float)Math.Round(lat, 5),
            Longitude: (float)Math.Round(lng, 5),
            TimeZone: "America/New_York",

            Locale: "en-US",

            // Slight bias toward dark mode
            PrefersDarkMode: rnd.NextDouble() < 0.7
        );
    }
}
