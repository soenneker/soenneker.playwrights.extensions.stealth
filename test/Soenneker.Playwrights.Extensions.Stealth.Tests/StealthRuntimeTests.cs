using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Playwright;
using Soenneker.Playwrights.Extensions.Stealth.Options;
using Soenneker.Tests.Attributes.Local;

namespace Soenneker.Playwrights.Extensions.Stealth.Tests;

public sealed class StealthRuntimeTests
{
    [Test]
    [LocalOnly]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RuntimeStackCaptureIsConfiguredWithoutReplacingJavaScript(bool hardened)
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.LaunchStealthChromium(
            new BrowserTypeLaunchOptions { Headless = true }, new StealthLaunchOptions { HardenRuntimeStackCapture = hardened });
        await using IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();
        ICDPSession session = await context.NewCDPSessionAsync(page);
        try
        {
            // Exercise repeated Runtime.enable on the same attached session too.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var captured = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
                session.Event("Runtime.exceptionThrown").OnEvent += (_, value) => captured.TrySetResult(value!.Value.Clone());
                await session.SendAsync("Runtime.enable");
                await session.SendAsync("Runtime.evaluate", new Dictionary<string, object> { ["expression"] = "setTimeout(() => { function probe() { throw new Error('probe'); } probe(); }, 0)" });
                JsonElement message = await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
                JsonElement details = message.GetProperty("exceptionDetails");
                int frames = details.TryGetProperty("stackTrace", out JsonElement stack) ? stack.GetProperty("callFrames").GetArrayLength() : 0;
                if (hardened)
                    frames.Should().Be(0);
                else
                    frames.Should().BeGreaterThan(0);
                details.GetProperty("exception").GetProperty("description").GetString().Should().Contain("probe");
                await session.SendAsync("Runtime.discardConsoleEntries");
                await session.SendAsync("Runtime.disable");
            }
        }
        finally { await session.DetachAsync(); }

        JsonElement native = await page.EvaluateAsync<JsonElement>("""
            () => {
              function preservedStack() { return new Error('preserved').stack; }
              return {
                stack: preservedStack(),
                errorNative: Function.prototype.toString.call(Error).includes('[native code]'),
                clockNative: Function.prototype.toString.call(performance.now).includes('[native code]'),
                stackLimit: Error.stackTraceLimit
              };
            }
            """);
        native.GetProperty("stack").GetString().Should().Contain("preservedStack");
        native.GetProperty("errorNative").GetBoolean().Should().BeTrue();
        native.GetProperty("clockNative").GetBoolean().Should().BeTrue();
        native.GetProperty("stackLimit").GetInt32().Should().Be(10);
    }

    [Test]
    [LocalOnly]
    public async Task HardenedBrowserSupportsPagesFramesWorkersAndClosesItsProcess()
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions { Headless = true });
        browser.Contexts.Should().BeEmpty();
        ICDPSession browserSession = await browser.NewBrowserCDPSessionAsync();
        JsonElement? processInfo = await browserSession.SendAsync("SystemInfo.getProcessInfo");
        int processId = processInfo!.Value.GetProperty("processInfo").EnumerateArray()
                                   .First(item => item.GetProperty("type").GetString() == "browser").GetProperty("id").GetInt32();
        using Process process = Process.GetProcessById(processId);
        try
        {
            await browserSession.SendAsync("Runtime.enable");
            throw new InvalidOperationException("The browser target should not expose Runtime.");
        }
        catch (PlaywrightException) { }
        browser.IsConnected.Should().BeTrue();
        await browserSession.DetachAsync();
        await using IBrowserContext context = await browser.NewContextAsync();
        browser.Contexts.Should().ContainSingle();
        await context.RouteAsync("https://**/*", route => route.FulfillAsync(new RouteFulfillOptions
        {
            ContentType = "text/html",
            Body = "<input aria-label='Value'><button onclick=\"document.title='clicked'\">Click</button>"
        }));
        IPage page = await context.NewPageAsync();
        await page.GotoAsync("https://runtime.test/");
        await page.GetByLabel("Value").FillAsync("working");
        await page.GetByRole(AriaRole.Button).ClickAsync();
        (await page.TitleAsync()).Should().Be("clicked");
        (await page.GetByLabel("Value").InputValueAsync()).Should().Be("working");
        await page.EvaluateAsync("() => { const f = document.createElement('iframe'); f.src = 'https://frame.test/'; document.body.append(f); }");
        await page.FrameLocator("iframe").GetByLabel("Value").FillAsync("frame working");
        (await page.FrameLocator("iframe").GetByLabel("Value").InputValueAsync()).Should().Be("frame working");
        IPage popup = await page.RunAndWaitForPopupAsync(() => page.EvaluateAsync("window.open('https://runtime.test/popup')"));
        await popup.GetByLabel("Value").FillAsync("popup working");
        IWorker worker = await page.RunAndWaitForWorkerAsync(() => page.EvaluateAsync("window.testWorker = new Worker(URL.createObjectURL(new Blob(['self.answer = 42'], {type: 'text/javascript'})))"));
        (await worker.EvaluateAsync<int>("self.answer")).Should().Be(42);
        (await page.ScreenshotAsync()).Length.Should().BeGreaterThan(0);
        await popup.CloseAsync();
        await context.CloseAsync();
        await browser.CloseAsync();
        browser.IsConnected.Should().BeFalse();
        process.HasExited.Should().BeTrue();
        await browser.CloseAsync();
    }
}
