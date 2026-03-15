[![](https://img.shields.io/nuget/v/soenneker.playwrights.extensions.stealth.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.playwrights.extensions.stealth/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.playwrights.extensions.stealth/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.playwrights.extensions.stealth/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.playwrights.extensions.stealth.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.playwrights.extensions.stealth/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.playwrights.extensions.stealth/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.playwrights.extensions.stealth/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Playwrights.Extensions.Stealth
### A collection of Playwright extensions for more stealthy usage

## Installation

```
dotnet add package Soenneker.Playwrights.Extensions.Stealth
```

## Getting started

Use `Playwright.LaunchStealthChromium()` to create the Browser, then use `browser.CreateStealthContext()` to create the BrowserContext. 
Then use the BrowserContext as usual to create your page.

```
using Microsoft.Playwright;
using Soenneker.Playwrights.Extensions.Stealth;


using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions
{
    Headless = true,
    Args = ["--no-sandbox"]
});

var browserContext = await browser.CreateStealthContext();
var page = await browserContext.NewPageAsync();
```
