using Soenneker.Utils.Random;
using System;

namespace Soenneker.Playwrights.Extensions.Stealth;

/// <summary>
/// Represents a coherent hardware and location profile for a Playwright context.
/// This record encapsulates various hardware and geographical attributes to mimic a real user's environment.
/// </summary>
/// <param name="Cores">The number of CPU cores available.</param>
/// <param name="MemoryGb">The amount of available memory in gigabytes.</param>
/// <param name="Platform">The operating system platform (e.g., "Win32", "MacIntel").</param>
/// <param name="ScreenW">The width of the screen in pixels.</param>
/// <param name="ScreenH">The height of the screen in pixels.</param>
/// <param name="ChromeVersion">The version of the Chrome browser.</param>
/// <param name="Seed">A seed value used for generating consistent random data.</param>
/// <param name="Latitude">The geographical latitude.</param>
/// <param name="Longitude">The geographical longitude.</param>
/// <param name="TimeZone">The IANA time zone name (e.g., "America/New_York").</param>
public sealed record HardwareProfile(
    int Cores,
    int MemoryGb,
    string Platform,
    int ScreenW,
    int ScreenH,
    string ChromeVersion,
    int Seed,
    double Latitude,
    double Longitude, string TimeZone)
{
    /// <summary>
    /// Generates a new <see cref="HardwareProfile"/> with randomized or default values.
    /// </summary>
    /// <returns>A new <see cref="HardwareProfile"/> instance.</returns>
    public static HardwareProfile Generate()
    {
        // stable random seed per context
        int seed = RandomUtil.Next(int.MinValue, int.MaxValue);
        var rnd = new Random(seed);

        int[] cores = [4, 6, 8, 12, 16, 24];
        int[] memories = [8, 16, 32];
        (int w, int h)[] screens = [(1920, 1080), (2560, 1440), (3840, 2160)];

        /* NYC bounding box: 40.55 – 40.96 N,  -74.25 – -73.70 W */
        double lat = 40.55 + rnd.NextDouble() * (40.96 - 40.55);
        double lng = -74.25 + rnd.NextDouble() * (-73.70 + 74.25);

        return new HardwareProfile(Cores: cores[rnd.Next(cores.Length)], MemoryGb: memories[rnd.Next(memories.Length)], Platform: "Win32",
            ScreenW: screens[rnd.Next(screens.Length)].w, ScreenH: screens[rnd.Next(screens.Length)].h, ChromeVersion: "114.0.5735.133", Seed: seed,
            Latitude: Math.Round(lat, 5), Longitude: Math.Round(lng, 5), "America/New_York");
    }
}