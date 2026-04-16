using System.Collections.Generic;

namespace Soenneker.Playwrights.Extensions.Stealth.Options;

/// <summary>
/// Controls how launch arguments are normalized for stealth-oriented Chromium sessions.
/// </summary>
public sealed class StealthLaunchOptions
{
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
    /// </summary>
    public List<string>? AdditionalArguments { get; set; }
}
