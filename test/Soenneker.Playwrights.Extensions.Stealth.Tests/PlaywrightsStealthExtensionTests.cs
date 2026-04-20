using Microsoft.Playwright;
using Soenneker.Facts.Manual;
using Soenneker.Playwrights.Extensions.Stealth.Options;
using Soenneker.Playwrights.Installation.Abstract;
using Soenneker.Tests.FixturedUnit;
using Soenneker.Utils.Delay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Playwrights.Extensions.Stealth.Dtos;
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
    public void StealthContextOptions_Defaults_DoNotRandomizeGeolocation()
    {
        var options = new StealthContextOptions();

        Assert.False(options.RandomizeGeolocation);
        Assert.True(options.WarmupSpeechVoices);
        Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.UserAgentData);
        Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.PermissionsQuery);
        Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.DocumentFonts);
        Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.Canvas);
        Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.MediaDevices);
        Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.WebGl);
        Assert.False(options.PatchUserAgentData);
        Assert.False(options.PatchPermissionsQuery);
        Assert.False(options.PatchDocumentFonts);
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

    [Fact]
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

        Assert.Equal(userAgent, headers["user-agent"]);
        Assert.Equal("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"", headers["sec-ch-ua"]);
        Assert.Equal("\"145.0.0.0\"", headers["sec-ch-ua-full-version"]);
        Assert.Equal("\"Google Chrome\";v=\"145.0.0.0\", \"Not.A/Brand\";v=\"8.0.0.0\", \"Chromium\";v=\"145.0.0.0\"",
            headers["sec-ch-ua-full-version-list"]);
        Assert.Equal("\"Windows\"", headers["sec-ch-ua-platform"]);
        Assert.Equal("?0", headers["sec-ch-ua-mobile"]);
        Assert.Equal("\"\"", headers["sec-ch-ua-model"]);
    }

    [Fact]
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

        Assert.Equal(userAgent, headers["user-agent"]);
        Assert.Equal("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"", headers["sec-ch-ua"]);
        Assert.Equal("\"145.0.0.0\"", headers["sec-ch-ua-full-version"]);
        Assert.Equal("\"Google Chrome\";v=\"145.0.0.0\", \"Not.A/Brand\";v=\"8.0.0.0\", \"Chromium\";v=\"145.0.0.0\"",
            headers["sec-ch-ua-full-version-list"]);
        Assert.Equal("\"Windows\"", headers["sec-ch-ua-platform"]);
        Assert.Equal("?0", headers["sec-ch-ua-mobile"]);
    }

    [Fact]
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

        Assert.Equal(userAgent, headers["user-agent"]);
        Assert.Equal("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"", headers["sec-ch-ua"]);
        Assert.Equal("\"145.0.0.0\"", headers["sec-ch-ua-full-version"]);
        Assert.Equal("\"Android\"", headers["sec-ch-ua-platform"]);
        Assert.Equal("?1", headers["sec-ch-ua-mobile"]);
        Assert.Equal("\"Pixel 7\"", headers["sec-ch-ua-model"]);
    }

    [Fact]
    public void WithUserAgent_AlignsMobileProfileFromUserAgent()
    {
        const string userAgent =
            "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Mobile Safari/537.36";

        HardwareProfile profile = HardwareProfile.Generate().WithUserAgent(userAgent);

        Assert.True(profile.IsMobile);
        Assert.Equal(5, profile.MaxTouchPoints);
        Assert.Equal(412, profile.ScreenW);
        Assert.Equal(915, profile.ScreenH);
        Assert.Equal(2.625, profile.DevicePixelRatio);
        Assert.Equal("Pixel 7", profile.DeviceModel);
        Assert.Equal("arm", profile.Architecture);
        Assert.Equal("64", profile.Bitness);
    }

    [Fact]
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

        Assert.Equal(userAgent, parameters["userAgent"]);
        Assert.Equal("Linux armv8l", parameters["platform"]);
        Assert.Equal("en-US,en;q=0.9", parameters["acceptLanguage"]);

        var metadata = Assert.IsType<Dictionary<string, object>>(parameters["userAgentMetadata"]);
        Assert.Equal("Android", metadata["platform"]);
        Assert.Equal("13.0.0", metadata["platformVersion"]);
        Assert.Equal("145.0.0.0", metadata["fullVersion"]);
        Assert.Equal("Pixel 7", metadata["model"]);
        Assert.Equal(true, metadata["mobile"]);
        Assert.Equal("arm", metadata["architecture"]);
        Assert.Equal("64", metadata["bitness"]);
    }

    [Fact]
    public void BuildContextOptions_OnlyAddsGeolocationPermissionWhenRandomizationIsEnabled()
    {
        var profile = HardwareProfile.Generate();
        MethodInfo method = typeof(PlaywrightsStealthExtension).Assembly
                                                           .GetType("Soenneker.Playwrights.Extensions.Stealth.StealthContextConfigurator")!
                                                           .GetMethod("BuildContextOptions", BindingFlags.Static | BindingFlags.Public)!;

        var defaultContextOptions = (BrowserNewContextOptions)method.Invoke(null,
            [profile, new BrowserNewContextOptions(), new StealthContextOptions()])!;

        Assert.True(defaultContextOptions.Permissions is null ||
                    !defaultContextOptions.Permissions.Contains("geolocation", StringComparer.OrdinalIgnoreCase));

        var randomizedContextOptions = (BrowserNewContextOptions)method.Invoke(null,
            [profile, new BrowserNewContextOptions(), new StealthContextOptions { RandomizeGeolocation = true }])!;

        Assert.Contains("geolocation", randomizedContextOptions.Permissions!, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
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

        Assert.Contains("patchGetter(Navigator.prototype, 'userAgentData', () => undefined);", script);
        Assert.Contains("patchValue(navigator.permissions, 'query', async () => {", script);
        Assert.Contains("patchGetter(document, 'fonts', () => undefined);", script);
        Assert.Contains("patchValue(HTMLCanvasElement.prototype, 'toDataURL', function() {", script);
        Assert.Contains("patchValue(navigator.mediaDevices, 'enumerateDevices', async () => []);", script);
    }

    [Fact]
    public void CompatibilityBooleanFlags_MapToSurfaceModes()
    {
        var options = new StealthContextOptions();

        options.PatchUserAgentData = true;
        options.PatchPermissionsQuery = true;
        options.PatchDocumentFonts = true;
        options.PatchWebGl = true;

        Assert.Equal(StealthSurfaceMode.Spoofed, options.Surfaces.UserAgentData);
        Assert.Equal(StealthSurfaceMode.Spoofed, options.Surfaces.PermissionsQuery);
        Assert.Equal(StealthSurfaceMode.Spoofed, options.Surfaces.DocumentFonts);
        Assert.Equal(StealthSurfaceMode.Spoofed, options.Surfaces.WebGl);
    }

    [Fact]
    public void StealthLaunchOptions_Defaults_Channel_IsChromium()
    {
        var options = new StealthLaunchOptions();

        Assert.Equal("chromium", options.Channel);
    }

    [Fact]
    public void LaunchStealthChromium_HasEndpointOverload()
    {
        MethodInfo? method = typeof(PlaywrightsStealthExtension).GetMethod(nameof(PlaywrightsStealthExtension.LaunchStealthChromium),
            [typeof(IPlaywright), typeof(string), typeof(BrowserTypeConnectOptions)]);

        Assert.NotNull(method);
    }

    [Fact]
    public void BuildScript_IncludesSpeechWarmup_ByDefault()
    {
        MethodInfo method = typeof(PlaywrightsStealthExtension).Assembly
                                                               .GetType("Soenneker.Playwrights.Extensions.Stealth.StealthScriptBuilder")!
                                                               .GetMethod("Build", BindingFlags.Static | BindingFlags.Public)!;

        var script = (string)method.Invoke(null, [HardwareProfile.Generate(), new StealthContextOptions()])!;

        Assert.Contains("typeof speechSynthesis !== 'undefined'", script);
        Assert.Contains("const voices = synth.getVoices();", script);
        Assert.Contains("voiceschanged", script);
    }

    [Fact]
    public void BuildScript_OmitsSpeechWarmup_WhenDisabled()
    {
        MethodInfo method = typeof(PlaywrightsStealthExtension).Assembly
                                                               .GetType("Soenneker.Playwrights.Extensions.Stealth.StealthScriptBuilder")!
                                                               .GetMethod("Build", BindingFlags.Static | BindingFlags.Public)!;

        var script = (string)method.Invoke(null, [HardwareProfile.Generate(), new StealthContextOptions
        {
            WarmupSpeechVoices = false
        }])!;

        Assert.DoesNotContain("typeof speechSynthesis !== 'undefined'", script);
        Assert.DoesNotContain("const voices = synth.getVoices();", script);
    }

    [ManualFact]
   //[LocalFact] 
   public async ValueTask NavigateToWebsite_WithStealth()
    {
        await _util.EnsureInstalled(CancellationToken);

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
