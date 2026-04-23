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
using Xunit;

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

        Xunit.Assert.False(options.RandomizeGeolocation);
        Xunit.Assert.True(options.WarmupSpeechVoices);
        Xunit.Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.UserAgentData);
        Xunit.Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.PermissionsQuery);
        Xunit.Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.DocumentFonts);
        Xunit.Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.Canvas);
        Xunit.Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.MediaDevices);
        Xunit.Assert.Equal(StealthSurfaceMode.Native, options.Surfaces.WebGl);
        Xunit.Assert.False(options.PatchUserAgentData);
        Xunit.Assert.False(options.PatchPermissionsQuery);
        Xunit.Assert.False(options.PatchDocumentFonts);
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

        Xunit.Assert.Contains("Chrome/147.0.0.0", userAgent);
        Xunit.Assert.DoesNotContain("Chrome/147.0.7727.15", userAgent);
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

        Xunit.Assert.Equal(userAgent, headers["user-agent"]);
        Xunit.Assert.Equal("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"", headers["sec-ch-ua"]);
        Xunit.Assert.Equal("\"145.0.0.0\"", headers["sec-ch-ua-full-version"]);
        Xunit.Assert.Equal("\"Google Chrome\";v=\"145.0.0.0\", \"Not.A/Brand\";v=\"8.0.0.0\", \"Chromium\";v=\"145.0.0.0\"",
            headers["sec-ch-ua-full-version-list"]);
        Xunit.Assert.Equal("\"Windows\"", headers["sec-ch-ua-platform"]);
        Xunit.Assert.Equal("?0", headers["sec-ch-ua-mobile"]);
        Xunit.Assert.Equal("\"\"", headers["sec-ch-ua-model"]);
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

        Xunit.Assert.Equal(userAgent, headers["user-agent"]);
        Xunit.Assert.Equal("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"", headers["sec-ch-ua"]);
        Xunit.Assert.Equal("\"145.0.0.0\"", headers["sec-ch-ua-full-version"]);
        Xunit.Assert.Equal("\"Google Chrome\";v=\"145.0.0.0\", \"Not.A/Brand\";v=\"8.0.0.0\", \"Chromium\";v=\"145.0.0.0\"",
            headers["sec-ch-ua-full-version-list"]);
        Xunit.Assert.Equal("\"Windows\"", headers["sec-ch-ua-platform"]);
        Xunit.Assert.Equal("?0", headers["sec-ch-ua-mobile"]);
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

        Xunit.Assert.Equal(userAgent, headers["user-agent"]);
        Xunit.Assert.Equal("\"Google Chrome\";v=\"145\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"145\"", headers["sec-ch-ua"]);
        Xunit.Assert.Equal("\"145.0.0.0\"", headers["sec-ch-ua-full-version"]);
        Xunit.Assert.Equal("\"Android\"", headers["sec-ch-ua-platform"]);
        Xunit.Assert.Equal("?1", headers["sec-ch-ua-mobile"]);
        Xunit.Assert.Equal("\"Pixel 7\"", headers["sec-ch-ua-model"]);
    }

    [Test]
    public void WithUserAgent_AlignsMobileProfileFromUserAgent()
    {
        const string userAgent =
            "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Mobile Safari/537.36";

        HardwareProfile profile = HardwareProfile.Generate().WithUserAgent(userAgent);

        Xunit.Assert.True(profile.IsMobile);
        Xunit.Assert.Equal(5, profile.MaxTouchPoints);
        Xunit.Assert.Equal(412, profile.ScreenW);
        Xunit.Assert.Equal(915, profile.ScreenH);
        Xunit.Assert.Equal(2.625, profile.DevicePixelRatio);
        Xunit.Assert.Equal("Pixel 7", profile.DeviceModel);
        Xunit.Assert.Equal("arm", profile.Architecture);
        Xunit.Assert.Equal("64", profile.Bitness);
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

        Xunit.Assert.Equal(userAgent, parameters["userAgent"]);
        Xunit.Assert.Equal("Linux armv8l", parameters["platform"]);
        Xunit.Assert.Equal("en-US,en;q=0.9", parameters["acceptLanguage"]);

        var metadata = Xunit.Assert.IsType<Dictionary<string, object>>(parameters["userAgentMetadata"]);
        Xunit.Assert.Equal("Android", metadata["platform"]);
        Xunit.Assert.Equal("13.0.0", metadata["platformVersion"]);
        Xunit.Assert.Equal("145.0.0.0", metadata["fullVersion"]);
        Xunit.Assert.Equal("Pixel 7", metadata["model"]);
        Xunit.Assert.Equal(true, metadata["mobile"]);
        Xunit.Assert.Equal("arm", metadata["architecture"]);
        Xunit.Assert.Equal("64", metadata["bitness"]);
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

        Xunit.Assert.True(defaultContextOptions.Permissions is null ||
                    !defaultContextOptions.Permissions.Contains("geolocation", StringComparer.OrdinalIgnoreCase));

        var randomizedContextOptions = (BrowserNewContextOptions)method.Invoke(null,
            [profile, new BrowserNewContextOptions(), new StealthContextOptions { RandomizeGeolocation = true }])!;

        Xunit.Assert.Contains("geolocation", randomizedContextOptions.Permissions!, StringComparer.OrdinalIgnoreCase);
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

        Xunit.Assert.Contains("patchGetter(Navigator.prototype, 'userAgentData', () => undefined);", script);
        Xunit.Assert.Contains("patchValue(navigator.permissions, 'query', async () => {", script);
        Xunit.Assert.Contains("patchGetter(document, 'fonts', () => undefined);", script);
        Xunit.Assert.Contains("patchValue(HTMLCanvasElement.prototype, 'toDataURL', function() {", script);
        Xunit.Assert.Contains("patchValue(navigator.mediaDevices, 'enumerateDevices', async () => []);", script);
    }

    [Test]
    public void CompatibilityBooleanFlags_MapToSurfaceModes()
    {
        var options = new StealthContextOptions();

        options.PatchUserAgentData = true;
        options.PatchPermissionsQuery = true;
        options.PatchDocumentFonts = true;
        options.PatchWebGl = true;

        Xunit.Assert.Equal(StealthSurfaceMode.Spoofed, options.Surfaces.UserAgentData);
        Xunit.Assert.Equal(StealthSurfaceMode.Spoofed, options.Surfaces.PermissionsQuery);
        Xunit.Assert.Equal(StealthSurfaceMode.Spoofed, options.Surfaces.DocumentFonts);
        Xunit.Assert.Equal(StealthSurfaceMode.Spoofed, options.Surfaces.WebGl);
    }

    [Test]
    public void StealthLaunchOptions_Defaults_Channel_IsChromium()
    {
        var options = new StealthLaunchOptions();

        Xunit.Assert.Equal("chromium", options.Channel);
    }

    [Test]
    public void LaunchStealthChromium_HasEndpointOverload()
    {
        MethodInfo? method = typeof(PlaywrightsStealthExtension).GetMethod(nameof(PlaywrightsStealthExtension.LaunchStealthChromium),
            [typeof(IPlaywright), typeof(string), typeof(BrowserTypeConnectOptions)]);

        Xunit.Assert.NotNull(method);
    }

    [Test]
    public void BuildScript_IncludesSpeechWarmup_ByDefault()
    {
        MethodInfo method = typeof(PlaywrightsStealthExtension).Assembly
                                                               .GetType("Soenneker.Playwrights.Extensions.Stealth.StealthScriptBuilder")!
                                                               .GetMethod("Build", BindingFlags.Static | BindingFlags.Public)!;

        var script = (string)method.Invoke(null, [HardwareProfile.Generate(), new StealthContextOptions()])!;

        Xunit.Assert.Contains("typeof speechSynthesis !== 'undefined'", script);
        Xunit.Assert.Contains("const voices = synth.getVoices();", script);
        Xunit.Assert.Contains("voiceschanged", script);
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

        Xunit.Assert.DoesNotContain("typeof speechSynthesis !== 'undefined'", script);
        Xunit.Assert.DoesNotContain("const voices = synth.getVoices();", script);
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
