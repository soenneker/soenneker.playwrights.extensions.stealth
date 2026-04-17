namespace Soenneker.Playwrights.Extensions.Stealth.Options;

/// <summary>
/// Controls stealth behavior for individual browser fingerprint surfaces.
/// </summary>
public sealed class StealthSurfaceOptions
{
    /// <summary>
    /// Controls the injected <c>navigator.userAgentData</c> behavior.
    /// </summary>
    public StealthSurfaceMode UserAgentData { get; set; } = StealthSurfaceMode.Native;

    /// <summary>
    /// Controls the injected <c>navigator.permissions.query()</c> behavior.
    /// </summary>
    public StealthSurfaceMode PermissionsQuery { get; set; } = StealthSurfaceMode.Native;

    /// <summary>
    /// Controls the injected <c>document.fonts</c> behavior.
    /// </summary>
    public StealthSurfaceMode DocumentFonts { get; set; } = StealthSurfaceMode.Native;

    /// <summary>
    /// Controls the injected canvas behavior.
    /// </summary>
    public StealthSurfaceMode Canvas { get; set; } = StealthSurfaceMode.Native;

    /// <summary>
    /// Controls the injected media devices behavior.
    /// </summary>
    public StealthSurfaceMode MediaDevices { get; set; } = StealthSurfaceMode.Native;

    /// <summary>
    /// Controls WebGL <c>UNMASKED_VENDOR_WEBGL</c> / <c>UNMASKED_RENDERER_WEBGL</c> spoofing.
    /// Defaults to <see cref="StealthSurfaceMode.Native"/> so vendor/renderer stay consistent with the real GPU
    /// when other surfaces are native (e.g. fingerprint checks that hash WebGL parameters).
    /// </summary>
    public StealthSurfaceMode WebGl { get; set; } = StealthSurfaceMode.Native;
}
