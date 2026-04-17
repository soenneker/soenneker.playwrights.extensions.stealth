namespace Soenneker.Playwrights.Extensions.Stealth.Options;

/// <summary>
/// Controls how an individual fingerprint surface should behave.
/// </summary>
public enum StealthSurfaceMode
{
    /// <summary>
    /// Leave the browser's native behavior untouched.
    /// </summary>
    Native,

    /// <summary>
    /// Apply the package's stealth spoofing or shim for the surface.
    /// </summary>
    Spoofed,

    /// <summary>
    /// Actively suppress or neutralize the surface.
    /// </summary>
    Disabled
}
