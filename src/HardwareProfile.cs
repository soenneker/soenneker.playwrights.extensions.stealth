using Soenneker.Utils.Random;

namespace Soenneker.Playwrights.Extensions.Stealth;

public sealed record HardwareProfile(int Cores, int MemoryGb, string Platform, int ScreenW, int ScreenH, string ChromeVersion, int Seed)
{
    public static HardwareProfile Generate(string tz)
    {
        // stable random seed per context
        int seed = RandomUtil.Next(int.MinValue, int.MaxValue);

        int[] corePool = [4, 6, 8, 12, 16, 24];
        int[] memoryPool = [8, 16, 32];
        (int w, int h)[] screenPool = [(1920, 1080), (2560, 1440), (3840, 2160)];

        return new HardwareProfile(Cores: corePool[RandomUtil.Next(corePool.Length)], MemoryGb: memoryPool[RandomUtil.Next(memoryPool.Length)], Platform: "Win32",
            ScreenW: screenPool[RandomUtil.Next(screenPool.Length)].w, ScreenH: screenPool[RandomUtil.Next(screenPool.Length)].h, ChromeVersion: "114.0.5735.133",
            Seed: seed);
    }
}