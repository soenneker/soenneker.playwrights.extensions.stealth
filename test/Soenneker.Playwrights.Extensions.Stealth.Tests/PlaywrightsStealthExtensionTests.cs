using Microsoft.Playwright;
using Soenneker.Tests.FixturedUnit;
using System.Threading.Tasks;
using Soenneker.Facts.Local;
using Soenneker.Playwright.Installation.Abstract;
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

    [LocalFact]
    public async ValueTask NavigateToWebsite_WithStealth()
    {
        await _util.EnsureInstalled(CancellationToken);

        using IPlaywright playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using IBrowser browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions { Headless = false });
        await using IBrowserContext context = await browser.CreateStealthContext();
        IPage page = await context.NewPageAsync();

        await page.GotoAsync("https://pixelscan.net/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        await DelayUtil.Delay(20000);
    }

    [LocalFact]
    public async ValueTask NavigateToWebsite_WithoutStealth()
    {
        await _util.EnsureInstalled(CancellationToken);

        using IPlaywright playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        await using IBrowserContext context = await browser.NewContextAsync();

        IPage page = await context.NewPageAsync();

        await page.GotoAsync("https://pixelscan.net/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        await DelayUtil.Delay(20000);
    }
}