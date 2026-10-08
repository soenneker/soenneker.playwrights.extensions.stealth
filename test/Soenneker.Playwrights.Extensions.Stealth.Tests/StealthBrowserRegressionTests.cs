using System.Text.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Playwright;
using Soenneker.Tests.Attributes.Local;
using Soenneker.Playwrights.Extensions.Stealth.Dtos;

namespace Soenneker.Playwrights.Extensions.Stealth.Tests;

public sealed class StealthBrowserRegressionTests
{
    [Test]
    [LocalOnly]
    [Arguments(null)]
    [Arguments("en-US")]
    [Arguments("fr-FR")]
    public async Task NativeSurfacesAndWorkersRemainConsistent(string? locale)
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = locale is null ? null : [$"--accept-lang={locale}"]
        });
        await using IBrowserContext context = await browser.CreateStealthContext(new BrowserNewContextOptions { Locale = locale });
        await context.RouteAsync("https://stealth.test/**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            ContentType = route.Request.Url.EndsWith(".js") ? "text/javascript" : "text/html",
            Body = route.Request.Url.EndsWith(".js")
                ? "onmessage = () => postMessage([...navigator.languages]);"
                : "<!doctype html><title>Stealth regression</title>"
        }));
        IPage page = await context.NewPageAsync();
        await page.GotoAsync("https://stealth.test/");

        JsonElement result = await page.EvaluateAsync<JsonElement>("""
            async () => {
              const blob = new Blob(['hello', new Uint8Array([33])], { type: 'text/plain' });
              class CustomBlob extends Blob {}
              class CustomWorker extends Worker {}
              const source = `postMessage({
                language: navigator.language,
                languages: [...navigator.languages],
                webdriverPresent: 'webdriver' in navigator,
                nativeConsole: Function.prototype.toString.call(console.log).includes('[native code]')
              });`;
              const url = URL.createObjectURL(new Blob([source], { type: 'text/javascript' }));
              const worker = new CustomWorker(url);
              let workerValues;
              try {
                workerValues = await new Promise((resolve, reject) => {
                  const timer = setTimeout(() => reject(new Error('Worker timed out')), 5000);
                  worker.onmessage = event => { clearTimeout(timer); resolve(event.data); };
                  worker.onerror = event => { clearTimeout(timer); reject(new Error(event.message)); };
                });
              } finally {
                worker.terminate();
                URL.revokeObjectURL(url);
              }
              const networkWorker = new Worker('/worker.js');
              let networkLanguages;
              try {
                networkLanguages = await new Promise((resolve, reject) => {
                  const timer = setTimeout(() => reject(new Error('Network worker timed out')), 5000);
                  networkWorker.onmessage = event => { clearTimeout(timer); resolve(event.data); };
                  networkWorker.onerror = event => { clearTimeout(timer); reject(new Error(event.message)); };
                  networkWorker.postMessage('languages');
                });
              } finally {
                networkWorker.terminate();
              }
              const media = matchMedia('(prefers-color-scheme: dark)');
              const voices = speechSynthesis.getVoices();
              return {
                language: navigator.language,
                languages: [...navigator.languages],
                workerValues,
                networkLanguages,
                blobText: await blob.text(),
                blobSubclass: new CustomBlob(['test']) instanceof CustomBlob,
                workerSubclass: worker instanceof CustomWorker,
                nativeFunctions: [console.log, Worker, Blob, URL.createObjectURL, matchMedia, speechSynthesis.getVoices]
                  .every(fn => Function.prototype.toString.call(fn).includes('[native code]')),
                nativeVoices: voices.every(voice => voice instanceof SpeechSynthesisVoice),
                nativeMedia: media instanceof MediaQueryList && media.dispatchEvent(new Event('change')),
                fabricatedChromeObjects: !!chrome.webstore || !!chrome.runtime,
                webdriver: navigator.webdriver
              };
            }
            """);

        string expectedLocale = locale ?? HardwareProfile.Generate().Locale;
        result.GetProperty("language").GetString().Should().Be(expectedLocale);
        result.GetProperty("networkLanguages").GetRawText().Should().Be(result.GetProperty("languages").GetRawText());
        JsonElement worker = result.GetProperty("workerValues");
        worker.GetProperty("language").GetString().Should().Be(expectedLocale);
        worker.GetProperty("languages").GetRawText().Should().Be(result.GetProperty("languages").GetRawText());
        worker.GetProperty("webdriverPresent").GetBoolean().Should().BeFalse();
        worker.GetProperty("nativeConsole").GetBoolean().Should().BeTrue();
        result.GetProperty("blobText").GetString().Should().Be("hello!");
        foreach (string property in new[] { "blobSubclass", "workerSubclass", "nativeFunctions", "nativeVoices", "nativeMedia" })
            result.GetProperty(property).GetBoolean().Should().BeTrue(property);
        result.GetProperty("fabricatedChromeObjects").GetBoolean().Should().BeFalse();
        result.GetProperty("webdriver").GetBoolean().Should().BeFalse();
    }
}
