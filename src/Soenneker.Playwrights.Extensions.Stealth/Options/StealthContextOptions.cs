using System.Collections.Generic;
using Microsoft.Playwright;

namespace Soenneker.Playwrights.Extensions.Stealth.Options;

/// <summary>
/// Controls optional stealth behaviors applied after a browser context has been created.
/// </summary>
public sealed class StealthContextOptions
{
    /// <summary>
    /// Optional proxy configuration passed into the new context.
    /// </summary>
    public Proxy? Proxy { get; set; }

    /// <summary>
    /// Merge these headers into the generated context headers.
    /// </summary>
    public IDictionary<string, string>? AdditionalHttpHeaders { get; set; }

    /// <summary>
    /// Include synthetic Client Hints request headers at the context level.
    /// Defaults to <c>false</c> because unconditional CH headers are easy to fingerprint.
    /// </summary>
    public bool InjectClientHintHeaders { get; set; }

    /// <summary>
    /// Normalize navigation request headers through context routing.
    /// </summary>
    public bool NormalizeDocumentHeaders { get; set; } = true;

    /// <summary>
    /// Align Playwright context color scheme with the generated profile.
    /// </summary>
    public bool AlignColorScheme { get; set; } = true;

    /// <summary>
    /// Apply optional CDP domain hardening hooks on each page (Chromium-only).
    /// </summary>
    public bool EnableCdpDomainHardening { get; set; }

    /// <summary>
    /// Disable the CDP Console domain for hardened pages.
    /// </summary>
    public bool DisableConsoleDomain { get; set; } = true;

    /// <summary>
    /// Disable the CDP Runtime domain for hardened pages.
    /// This can break Playwright features and is disabled by default.
    /// </summary>
    public bool DisableRuntimeDomain { get; set; }
}
