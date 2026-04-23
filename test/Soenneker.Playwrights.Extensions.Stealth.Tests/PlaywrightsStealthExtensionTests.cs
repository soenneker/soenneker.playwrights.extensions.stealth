using Microsoft.Playwright;
using Soenneker.Playwrights.Extensions.Stealth.Options;
using Soenneker.Playwrights.Installation.Abstract;
using Soenneker.Tests.HostedUnit;
using Soenneker.Utils.Delay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Playwrights.Extensions.Stealth.Dtos;
using AwesomeAssertions;

namespace Soenneker.Playwrights.Extensions.Stealth.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class PlaywrightsStealthExtensionTests : HostedUnitTest
{
    private readonly IPlaywrightInstallationUtil _util;

    public PlaywrightsStealthExtensionTests(Host host) : base(host)
    {
        _util = Resolve<IPlaywrightInstallationUtil>();
    }

    [Test]
    public void Default()
    {
    }

    [Test]
    public void StealthContextOptions_Defaults_DoNotRandomizeGeolocation()
    {
        var options = new StealthContextOptions();

        options.RandomizeGeolocation.Should().BeFalse();
        options.WarmupSpeechVoices.Should().BeTrue();
        options.Surfaces.UserAgentData.Should().Be(StealthSurfaceMode.Native);
        options.Surfaces.PermissionsQuery.Should().Be(StealthSurfaceMode.Native);
        options.Surfaces.DocumentFonts.Should().Be(StealthSurfaceMode.Native);
        options.Surfaces.Canvas.Should().Be(StealthSurfaceMode.Native);
        options.Surfaces.MediaDevices.Should().Be(StealthSurfaceMode.Native);
        options.Surfaces.WebGl.Should().Be(StealthSurfaceMode.Native);
        options.PatchUserAgentData.Should().BeFalse();
        options.PatchPermissionsQuery.Should().BeFalse();
        options.PatchDocumentFonts.Should().BeFalse();
    }

    [Test]
    public void BuildUserAgent_UsesReducedChromiumVersion()
    {
        HardwareProfile profile = HardwareProfile.Generate() with
        {
            ChromeVersion = "147.0.7727.15",
            ChromeMajorVersion = 147
        };

        string userAgent = StealthHeaderBuilder.BuildUserAgent(profile);

        userAgent.Should().Contain("Chrome/147.0.0.0");
        userAgent.Should().NotContain("Chrome/147.0.7727.15");
    }

    [Test]
    public void BuildContextHeaders_UsesAssignedUserAgentVersionForClientHints()
    {
        const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36";

        HardwareProfile profile = HardwareProfile.Generate() with
        {
            ChromeVersion = "147.0.7727.15",
            ChromeMajorVersion = 147
        };

        HardwareProfile effectiveProfile = profile.WithUserAgent(userAgent);

        Dictionary<string, string> headers = StealthHeaderBuilder.BuildContextHeaders(effectiveProfile, new StealthContextOptions
        {
            InjectClientHintHeaders = true
        });

        headers["user-agent"].Should().Be(userAgent);
        headers["sec-ch-ua"].Should().Be("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"");
        headers["sec-ch-ua-full-version"].Should().Be("\"145.0.0.0\"");
        headers["sec-ch-ua-full-version-list"].Should().Be("\"Google Chrome\";v=\"145.0.0.0\", \"Not.A/Brand\";v=\"8.0.0.0\", \"Chromium\";v=\"145.0.0.0\"");
        headers["sec-ch-ua-platform"].Should().Be("\"Windows\"");
        headers["sec-ch-ua-mobile"].Should().Be("?0");
        headers["sec-ch-ua-model"].Should().Be("\"\"");
    }

    [Test]
    public void BuildDocumentHeaders_RewritesClientHintsUsingAssignedUserAgentVersion()
    {
        const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36";

        HardwareProfile profile = HardwareProfile.Generate() with
        {
            ChromeVersion = "147.0.7727.15",
            ChromeMajorVersion = 147
        };

        HardwareProfile effectiveProfile = profile.WithUserAgent(userAgent);

        var requestHeaders = new Dictionary<string, string>
        {
            ["referer"] = "https://example.com/start",
            ["user-agent"] = userAgent,
            ["sec-ch-ua"] = "\"Chromium\";v=\"147\", \"Not.A/Brand\";v=\"8\""
        };

        Dictionary<string, string> headers = StealthHeaderBuilder.BuildDocumentHeaders(
            effectiveProfile,
            requestHeaders,
            "https://example.com/next",
            new StealthContextOptions
            {
                InjectClientHintHeaders = true
            });

        headers["user-agent"].Should().Be(userAgent);
        headers["sec-ch-ua"].Should().Be("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"");
        headers["sec-ch-ua-full-version"].Should().Be("\"145.0.0.0\"");
        headers["sec-ch-ua-full-version-list"].Should().Be("\"Google Chrome\";v=\"145.0.0.0\", \"Not.A/Brand\";v=\"8.0.0.0\", \"Chromium\";v=\"145.0.0.0\"");
        headers["sec-ch-ua-platform"].Should().Be("\"Windows\"");
        headers["sec-ch-ua-mobile"].Should().Be("?0");
    }

    [Test]
    public void BuildContextHeaders_UsesAssignedMobileUserAgentPlatformAndMobileState()
    {
        const string userAgent =
            "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Mobile Safari/537.36";

        HardwareProfile profile = HardwareProfile.Generate() with
        {
            ChromeVersion = "147.0.7727.15",
            ChromeMajorVersion = 147
        };

        HardwareProfile effectiveProfile = profile.WithUserAgent(userAgent);

        Dictionary<string, string> headers = StealthHeaderBuilder.BuildContextHeaders(effectiveProfile, new StealthContextOptions
        {
            InjectClientHintHeaders = true
        });

        headers["user-agent"].Should().Be(userAgent);
        headers["sec-ch-ua"].Should().Be("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"");
        headers["sec-ch-ua-full-version"].Should().Be("\"145.0.0.0\"");
        headers["sec-ch-ua-platform"].Should().Be("\"Android\"");
        headers["sec-ch-ua-mobile"].Should().Be("?1");
        headers["sec-ch-ua-model"].Should().Be("\"Pixel 7\"");
    }

    [Test]
    public void WithUserAgent_AlignsMobileProfileFromUserAgent()
    {
        const string userAgent =
            "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Mobile Safari/537.36";

        HardwareProfile profile = HardwareProfile.Generate().WithUserAgent(userAgent);

        profile.IsMobile.Should().BeTrue();
        profile.MaxTouchPoints.Should().Be(5);
        profile.ScreenW.Should().Be(412);
        profile.ScreenH.Should().Be(915);
        profile.DevicePixelRatio.Should().Be(2.625);
        profile.DeviceModel.Should().Be("Pixel 7");
        profile.Architecture.Should().Be("arm");
        profile.Bitness.Should().Be("64");
    }

    [Test]
    public void BuildUserAgentOverrideParameters_UsesChromiumMetadataAlignedWithProfile()
    {
        const string userAgent =
            "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Mobile Safari/537.36";

        HardwareProfile profile = (HardwareProfile.Generate() with
        {
            Locale = "en-US",
            Languages = ["en-US", "en"]
        }).WithUserAgent(userAgent);

        Dictionary<string, object> parameters = StealthHeaderBuilder.BuildUserAgentOverrideParameters(profile);

        parameters["userAgent"].Should().Be(userAgent);
        parameters["platform"].Should().Be("Linux armv8l");
        parameters["acceptLanguage"].Should().Be("en-US,en;q=0.9");

        var metadata = parameters["userAgentMetadata"].Should().BeOfType<Dictionary<string, object>>().Which;
        metadata["platform"].Should().Be("Android");
        metadata["platformVersion"].Should().Be("13.0.0");
        metadata["fullVersion"].Should().Be("145.0.0.0");
        metadata["model"].Should().Be("Pixel 7");
        metadata["mobile"].Should().Be(true);
        metadata["architecture"].Should().Be("arm");
        metadata["bitness"].Should().Be("64");
    }

    [Test]
    public void BuildContextOptions_OnlyAddsGeolocationPermissionWhenRandomizationIsEnabled()
    {
        var profile = HardwareProfile.Generate();
        MethodInfo method = typeof(PlaywrightsStealthExtension).Assembly
                                                           .GetType("Soenneker.Playwrights.Extensions.Stealth.StealthContextConfigurator")!
                                                           .GetMethod("BuildContextOptions", BindingFlags.Static | BindingFlags.Public)!;

        var defaultContextOptions = (BrowserNewContextOptions)method.Invoke(null,
            [profile, new BrowserNewContextOptions(), new StealthContextOptions()])!;

        (defaultContextOptions.Permissions is null ||
                    !defaultContextOptions.Permissions.Contains("geolocation", StringComparer.OrdinalIgnoreCase)).Should().BeTrue();

        var randomizedContextOptions = (BrowserNewContextOptions)method.Invoke(null,
            [profile, new BrowserNewContextOptions(), new StealthContextOptions { RandomizeGeolocation = true }])!;

        randomizedContextOptions.Permissions!.Should().Contain(item => string.Equals(item, "geolocation", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void BuildScript_OmitsConfigurableFingerprintShims_WhenDisabled()
    {
        MethodInfo method = typeof(PlaywrightsStealthExtension).Assembly
                                                               .GetType("Soenneker.Playwrights.Extensions.Stealth.StealthScriptBuilder")!
                                                               .GetMethod("Build", BindingFlags.Static | BindingFlags.Public)!;

        var script = (string)method.Invoke(null,
            [HardwareProfile.Generate(), new StealthContextOptions
            {
                Surfaces = new StealthSurfaceOptions
                {
                    UserAgentData = StealthSurfaceMode.Disabled,
                    PermissionsQuery = StealthSurfaceMode.Disabled,
                    DocumentFonts = StealthSurfaceMode.Disabled,
                    Canvas = StealthSurfaceMode.Disabled,
                    MediaDevices = StealthSurfaceMode.Disabled
                }
            }])!;

        script.Should().Contain("patchGetter(Navigator.prototype, 'userAgentData', () => undefined);");
        script.Should().Contain("patchValue(navigator.permissions, 'query', async () => {");
        script.Should().Contain("patchGetter(document, 'fonts', () => undefined);");
        script.Should().Contain("patchValue(HTMLCanvasElement.prototype, 'toDataURL', function() {");
        script.Should().Contain("patchValue(navigator.mediaDevices, 'enumerateDevices', async () => []);");
    }

    [Test]
    public void CompatibilityBooleanFlags_MapToSurfaceModes()
    {
        var options = new StealthContextOptions();

        options.PatchUserAgentData = true;
        options.PatchPermissionsQuery = true;
        options.PatchDocumentFonts = true;
        options.PatchWebGl = true;

        options.Surfaces.UserAgentData.Should().Be(StealthSurfaceMode.Spoofed);
        options.Surfaces.PermissionsQuery.Should().Be(StealthSurfaceMode.Spoofed);
        options.Surfaces.DocumentFonts.Should().Be(StealthSurfaceMode.Spoofed);
        options.Surfaces.WebGl.Should().Be(StealthSurfaceMode.Spoofed);
    }

    [Test]
    public void StealthLaunchOptions_Defaults_Channel_IsChromium()
    {
        var options = new StealthLaunchOptions();

        options.Channel.Should().Be("chromium");
    }

    [Test]
    public void LaunchStealthChromium_HasEndpointOverload()
    {
        MethodInfo? method = typeof(PlaywrightsStealthExtension).GetMethod(nameof(PlaywrightsStealthExtension.LaunchStealthChromium),
            [typeof(IPlaywright), typeof(string), typeof(BrowserTypeConnectOptions)]);

        method.Should().NotBeNull();
    }

    [Test]
    public void BuildScript_IncludesSpeechWarmup_ByDefault()
    {
        MethodInfo method = typeof(PlaywrightsStealthExtension).Assembly
                                                               .GetType("Soenneker.Playwrights.Extensions.Stealth.StealthScriptBuilder")!
                                                               .GetMethod("Build", BindingFlags.Static | BindingFlags.Public)!;

        var script = (string)method.Invoke(null, [HardwareProfile.Generate(), new StealthContextOptions()])!;

        script.Should().Contain("typeof speechSynthesis !== 'undefined'");
        script.Should().Contain("const voices = synth.getVoices();");
        script.Should().Contain("voiceschanged");
    }

    [Test]
    public void BuildScript_OmitsSpeechWarmup_WhenDisabled()
    {
        MethodInfo method = typeof(PlaywrightsStealthExtension).Assembly
                                                               .GetType("Soenneker.Playwrights.Extensions.Stealth.StealthScriptBuilder")!
                                                               .GetMethod("Build", BindingFlags.Static | BindingFlags.Public)!;

        var script = (string)method.Invoke(null, [HardwareProfile.Generate(), new StealthContextOptions
        {
            WarmupSpeechVoices = false
        }])!;

        script.Should().NotContain("typeof speechSynthesis !== 'undefined'");
        script.Should().NotContain("const voices = synth.getVoices();");
    }

    [Skip("Manual")]
   //[LocalOnly] 
   public async ValueTask NavigateToWebsite_WithStealth()
    {
        await _util.EnsureInstalled(System.Threading.CancellationToken.None);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions { Headless = false });

        var stealthContextOptions = new StealthContextOptions
        {
            WarmupSpeechVoices = true,
            Surfaces = new StealthSurfaceOptions
            {
                UserAgentData = StealthSurfaceMode.Native,
                PermissionsQuery = StealthSurfaceMode.Native,
                DocumentFonts = StealthSurfaceMode.Native,
                Canvas = StealthSurfaceMode.Native,
                MediaDevices = StealthSurfaceMode.Native,
                WebGl = StealthSurfaceMode.Native
            }
        };

        await using IBrowserContext context = await browser.CreateStealthContext(new BrowserNewContextOptions(), stealthContextOptions);
        IPage page = await context.NewPageAsync();

        await page.GotoAsync("https://pixelscan.net/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        await Task.Delay(Timeout.InfiniteTimeSpan);
    }

    [Skip("Manual")]
    public async ValueTask NavigateToWebsite_WithoutStealth()
    {
        await _util.EnsureInstalled(System.Threading.CancellationToken.None);

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        await using IBrowserContext context = await browser.NewContextAsync();

        IPage page = await context.NewPageAsync();

        await page.GotoAsync("https://pixelscan.net/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        await DelayUtil.Delay(20000);
    }
}
