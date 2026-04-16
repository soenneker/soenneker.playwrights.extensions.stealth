using Microsoft.Playwright;
using Soenneker.Tests.FixturedUnit;
using System.Threading.Tasks;
using Soenneker.Facts.Manual;
using Soenneker.Playwrights.Installation.Abstract;
using Soenneker.Utils.Delay;
using Xunit;

namespace Soenneker.Playwrights.Extensions.Stealth.Tests;

[Collection("Collection")]
public sealed class PlaywrightsStealthExtensionTests : FixturedUnitTest
{
    private readonly IPlaywrightInstallationUtil _util;

    public PlaywrightsStealthExtensionTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
        _util = Resolve<IPlaywrightInstallationUtil>();
    }

    [Fact]
    public void Default()
    {
    }

    [Fact]
    public void BuildUserAgent_UsesReducedChromiumVersion()
    {
        HardwareProfile profile = HardwareProfile.Generate() with
        {
            ChromeVersion = "147.0.7727.15",
            ChromeMajorVersion = 147
        };

        string userAgent = StealthHeaderBuilder.BuildUserAgent(profile);

        Assert.Contains("Chrome/147.0.0.0", userAgent);
        Assert.DoesNotContain("Chrome/147.0.7727.15", userAgent);
    }

    [ManualFact]
    public async ValueTask NavigateToWebsite_WithStealth()
    {
        await _util.EnsureInstalled(CancellationToken);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions { Headless = false });
        await using IBrowserContext context = await browser.CreateStealthContext();
        IPage page = await context.NewPageAsync();

        await page.GotoAsync("https://pixelscan.net/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        await DelayUtil.Delay(20000);
    }

    [ManualFact]
    public async ValueTask NavigateToWebsite_WithoutStealth()
    {
        await _util.EnsureInstalled(CancellationToken);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        await using IBrowserContext context = await browser.NewContextAsync();

        IPage page = await context.NewPageAsync();

        await page.GotoAsync("https://pixelscan.net/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        await DelayUtil.Delay(20000);
    }
}