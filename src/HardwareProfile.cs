using Soenneker.Utils.Random;
using System;

namespace Soenneker.Playwrights.Extensions.Stealth;

/// <summary>
/// One coherent hardware & location profile per Playwright context.
/// </summary>
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