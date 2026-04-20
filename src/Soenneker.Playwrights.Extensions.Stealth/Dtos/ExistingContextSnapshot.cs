using System.Collections.Generic;

namespace Soenneker.Playwrights.Extensions.Stealth.Dtos;

internal sealed class ExistingContextSnapshot
{
    public string UserAgent { get; set; } = string.Empty;
    public string? Language { get; set; }
    public List<string>? Languages { get; set; }
    public string? Platform { get; set; }
    public int MaxTouchPoints { get; set; }
    public double DevicePixelRatio { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public string? TimeZone { get; set; }
}