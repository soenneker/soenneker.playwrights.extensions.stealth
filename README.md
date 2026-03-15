[![](https://img.shields.io/nuget/v/soenneker.playwrights.extensions.stealth.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.playwrights.extensions.stealth/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.playwrights.extensions.stealth/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.playwrights.extensions.stealth/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.playwrights.extensions.stealth.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.playwrights.extensions.stealth/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.playwrights.extensions.stealth/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.playwrights.extensions.stealth/actions/workflows/codeql.yml)

# Soenneker.Playwrights.Extensions.Stealth

A .NET extension library for [Microsoft Playwright](https://playwright.dev/dotnet/) that makes browser automation harder to detect. It applies launch-argument hardening, context shaping, and init-script evasions so Chromium sessions look more like normal user sessions.

## Installation

```bash
dotnet add package Soenneker.Playwrights.Extensions.Stealth
```

## Quick start

1. Launch Chromium with stealth defaults: `Playwright.LaunchStealthChromium()`.
2. Create a stealth browser context: `browser.CreateStealthContext()`.
3. Use the context as usual to create pages and run automation.

```csharp
using Microsoft.Playwright;
using Soenneker.Playwrights.Extensions.Stealth;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions
{
    Headless = true
});

var browserContext = await browser.CreateStealthContext();
var page = await browserContext.NewPageAsync();
```

### With proxy

Pass an optional proxy into `CreateStealthContext`:

```csharp
var context = await browser.CreateStealthContext(new Proxy
{
    Server = "http://proxy.example.com:8080",
    Username = "user",
    Password = "secret"
});
```

### Customizing launch and context

Tune stealth behavior without forking Playwright:

```csharp
using Microsoft.Playwright;
using Soenneker.Playwrights.Extensions.Stealth;
using Soenneker.Playwrights.Extensions.Stealth.Options;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.LaunchStealthChromium(
    new BrowserTypeLaunchOptions { Headless = true },
    new StealthLaunchOptions
    {
        IncludeNoSandboxArgument = false,
        IgnoreDetectableDefaultArguments = true,
        ForceNewHeadlessMode = true,
        AdditionalIgnoredDefaultArguments = ["--disable-features=DialMediaRouteProvider"],
        AdditionalArguments = ["--disable-features=Translate"]
    });

var context = await browser.CreateStealthContext(
    new BrowserNewContextOptions(),
    new StealthContextOptions
    {
        NormalizeDocumentHeaders = true,
        InjectClientHintHeaders = false,
        EnableCdpDomainHardening = true,
        DisableConsoleDomain = true,
        DisableRuntimeDomain = false,
        AdditionalHttpHeaders = new Dictionary<string, string>
        {
            ["DNT"] = "1"
        }
    });
```

### Applying stealth to an existing context

You can apply the same stealth behavior to a context you already created:

```csharp
var context = await browser.NewContextAsync();
await context.ApplyStealthAsync();
```

## Features

| Area | Description |
|------|-------------|
| **Launch options** | Normalizes Chromium args with stealth-oriented defaults: ensures `--disable-blink-features=AutomationControlled`, forces `--headless=new` when headless, and can strip detectable Playwright default args via `IgnoreDefaultArgs`. |
| **Hardware profile** | Each context gets a random but internally consistent Windows/Chrome profile: CPU cores, memory, viewport, DPR, Chrome version, timezone, locale/languages, WebGL identity, and geolocation region. |
| **Context shaping** | Sets coherent User-Agent, language headers, timezone, viewport, DPR, and color scheme from the same generated profile. Client Hints headers are opt-in. |
| **Request shaping** | Registers early context routing so top-level document navigations get normalized navigation headers before the first page load. |
| **CDP hardening (optional)** | Can disable selected Chromium CDP domains (e.g. Console, optionally Runtime) per page to reduce protocol surface. |
| **Init script** | Injected before page load to reduce automation signals: hides `navigator.webdriver`; aligns `navigator`/`screen`/`window` (e.g. `hardwareConcurrency`, `deviceMemory`, `platform`, `vendor`, `languages`, `plugins`, dimensions, DPR); Client Hints and `navigator.userAgentData`; `window.chrome`; permissions/connection/battery/media/geolocation shims; timezone via `Intl`; canvas noise; WebGL vendor/renderer spoofing; WebRTC host-candidate stripping. |

## Options reference

- **StealthLaunchOptions** — Controls how launch arguments are normalized (`RemoveDetectableArguments`, `IncludeNoSandboxArgument`, `IgnoreDetectableDefaultArguments`, `ForceNewHeadlessMode`, `AdditionalArguments`, `AdditionalIgnoredDefaultArguments`).
- **StealthContextOptions** — Controls context and request behavior (`Proxy`, `AdditionalHttpHeaders`, `InjectClientHintHeaders`, `NormalizeDocumentHeaders`, `AlignColorScheme`, `EnableCdpDomainHardening`, `DisableConsoleDomain`, `DisableRuntimeDomain`).