using System.Collections.Generic;

namespace Soenneker.Playwrights.Extensions.Stealth.Options;

/// <summary>
/// Controls how launch arguments are normalized for stealth-oriented Chromium sessions.
/// </summary>
public sealed class StealthLaunchOptions
{
    /// <summary>
    /// Disable inspector stack capture on Playwright's Chromium sessions to avoid depth-dependent
    /// error-creation overhead. Uses an owned loopback CDP connection and preserves JavaScript
    /// error stacks, clocks, and Runtime evaluation. Inspector-provided exception stacks may be absent.
    /// Set to <c>false</c> to use the standard Playwright transport. Applies to local launches only.
    /// </summary>
    public bool HardenRuntimeStackCapture { get; set; } = true;

    /// <summary>
    /// Remove arguments that are commonly associated with browser automation or unstable stealth defaults.
    /// </summary>
    public bool RemoveDetectableArguments { get; set; } = true;

    /// <summary>
    /// Include <c>--no-sandbox</c> in the normalized launch arguments.
    /// </summary>
    public bool IncludeNoSandboxArgument { get; set; } = true;

    /// <summary>
    /// Remove known Playwright default Chromium args through <c>IgnoreDefaultArgs</c>,
    /// stripping automation-signature defaults that are easy to detect.
    /// </summary>
    public bool IgnoreDetectableDefaultArguments { get; set; } = true;

    /// <summary>
    /// Additional Playwright default arguments to ignore during launch.
    /// </summary>
    public List<string>? AdditionalIgnoredDefaultArguments { get; set; }

    /// <summary>
    /// Additional arguments appended after the built-in stealth defaults have been normalized.
    /// For a custom context locale, include a matching <c>--accept-lang</c> (for example,
    /// <c>--accept-lang=fr-FR</c>) so URL-based workers inherit the same language.
    /// The default startup language matches the system locale used for generated contexts.
    /// </summary>
    public List<string>? AdditionalArguments { get; set; }

    /// <summary>
    /// Playwright browser channel when <c>BrowserTypeLaunchOptions.Channel</c> is not set
    /// (e.g. <c>chromium</c>, <c>chrome</c>, <c>msedge</c>). Defaults to <c>chromium</c>.
    /// </summary>
    public string Channel { get; set; } = "chromium";
}
