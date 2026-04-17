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
    public Dictionary<string, string>? AdditionalHttpHeaders { get; set; }

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
    /// Controls the stealth mode for configurable browser fingerprint surfaces.
    /// Defaults favor native behavior unless a surface is explicitly spoofed.
    /// </summary>
    public StealthSurfaceOptions Surfaces { get; set; } = new();

    /// <summary>
    /// Apply a randomized geolocation from the generated hardware profile and grant
    /// geolocation permission for the context. Defaults to <c>false</c> so stealth
    /// does not touch location unless explicitly requested.
    /// </summary>
    public bool RandomizeGeolocation { get; set; }

    /// <summary>
    /// Warm up native speech synthesis voices before page scripts run so voice enumeration
    /// is less likely to be observed in an uninitialized state.
    /// </summary>
    public bool WarmupSpeechVoices { get; set; } = true;

    /// <summary>
    /// Compatibility shim for <see cref="StealthSurfaceOptions.UserAgentData"/>.
    /// <c>true</c> maps to <see cref="StealthSurfaceMode.Spoofed"/> and <c>false</c>
    /// maps to <see cref="StealthSurfaceMode.Native"/>.
    /// </summary>
    public bool PatchUserAgentData
    {
        get => Surfaces.UserAgentData == StealthSurfaceMode.Spoofed;
        set => Surfaces.UserAgentData = value ? StealthSurfaceMode.Spoofed : StealthSurfaceMode.Native;
    }

    /// <summary>
    /// Compatibility shim for <see cref="StealthSurfaceOptions.PermissionsQuery"/>.
    /// <c>true</c> maps to <see cref="StealthSurfaceMode.Spoofed"/> and <c>false</c>
    /// maps to <see cref="StealthSurfaceMode.Native"/>.
    /// </summary>
    public bool PatchPermissionsQuery
    {
        get => Surfaces.PermissionsQuery == StealthSurfaceMode.Spoofed;
        set => Surfaces.PermissionsQuery = value ? StealthSurfaceMode.Spoofed : StealthSurfaceMode.Native;
    }

    /// <summary>
    /// Compatibility shim for <see cref="StealthSurfaceOptions.DocumentFonts"/>.
    /// <c>true</c> maps to <see cref="StealthSurfaceMode.Spoofed"/> and <c>false</c>
    /// maps to <see cref="StealthSurfaceMode.Native"/>.
    /// </summary>
    public bool PatchDocumentFonts
    {
        get => Surfaces.DocumentFonts == StealthSurfaceMode.Spoofed;
        set => Surfaces.DocumentFonts = value ? StealthSurfaceMode.Spoofed : StealthSurfaceMode.Native;
    }

    /// <summary>
    /// Compatibility shim for <see cref="StealthSurfaceOptions.WebGl"/>.
    /// </summary>
    public bool PatchWebGl
    {
        get => Surfaces.WebGl == StealthSurfaceMode.Spoofed;
        set => Surfaces.WebGl = value ? StealthSurfaceMode.Spoofed : StealthSurfaceMode.Native;
    }

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
