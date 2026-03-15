[![](https://img.shields.io/nuget/v/soenneker.playwrights.extensions.stealth.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.playwrights.extensions.stealth/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.playwrights.extensions.stealth/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.playwrights.extensions.stealth/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.playwrights.extensions.stealth.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.playwrights.extensions.stealth/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.playwrights.extensions.stealth/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.playwrights.extensions.stealth/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Playwrights.Extensions.Stealth
### A collection of Playwright extensions for more stealthy usage

## Installation

```bash
dotnet add package Soenneker.Playwrights.Extensions.Stealth
```

## Getting started

1. Launch Chromium with stealth flags using `Playwright.LaunchStealthChromium()`.
2. Create a stealth browser context with `browser.CreateStealthContext()`.
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

Optional proxy can be passed into `CreateStealthContext`:

```csharp
var context = await browser.CreateStealthContext(new Proxy
{
    Server = "http://proxy.example.com:8080",
    Username = "user",
    Password = "secret"
});
```

## What it does

- **Launch options** — Uses Chromium with stealth-related args (e.g. `--disable-blink-features=AutomationControlled`, `--enable-quic`, `--use-gl=desktop`).
- **Hardware profile** — Each context gets a random but consistent profile: CPU cores, device memory, viewport size, Chrome version, timezone (e.g. America/New_York), and locale.
- **Init script** — Injected before page load to reduce automation signals:
  - `navigator.webdriver` hidden
  - Realistic `navigator` (hardwareConcurrency, deviceMemory, platform, vendor) and Client Hints (`userAgentData`)
  - `window.chrome` stub, permissions query latency, `navigator.connection`, Battery API
  - Coherent timezone via `Intl`, font list, media devices, geolocation
  - Canvas fingerprint noise, WebGL vendor spoof, WebRTC local IP leak blocked
