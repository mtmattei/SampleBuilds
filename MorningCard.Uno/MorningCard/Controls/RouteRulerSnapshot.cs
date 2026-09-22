using SkiaSharp;

namespace MorningCard.Controls;

/// <summary>
/// Everything the renderer needs, captured on the UI thread. RenderOverride runs on the render
/// thread, so it reads plain fields and never touches a DependencyProperty or a theme resource.
/// </summary>
public sealed record RouteRulerSnapshot
{
    public bool HasRoute { get; init; }

    public bool IsLate { get; init; }

    /// <summary>
    /// The route in screen pixels, projected through the map's viewport on the UI thread. Null
    /// before the asset has loaded or the map has been fitted, which leaves only the tiles.
    /// </summary>
    public RulerProjection? Projection { get; init; }

    public string NowLabel { get; init; } = string.Empty;

    public string LeaveLabel { get; init; } = string.Empty;

    public string SlackLabel { get; init; } = string.Empty;

    public string DriveLabel { get; init; } = string.Empty;

    public string ArriveLabel { get; init; } = string.Empty;

    public string WalkLabel { get; init; } = string.Empty;

    public string BellLabel { get; init; } = string.Empty;

    /// <summary>0 to 1 across the whole entrance; 1 means settled.</summary>
    public double Progress { get; init; } = 1d;

    public RouteRulerPalette Palette { get; init; } = RouteRulerPalette.Fallback;

}

/// <summary>
/// The colours, snapshotted from the app's theme resources. The canvas draws straight onto the
/// window surface, so it cannot inherit brushes or opacity from XAML.
/// </summary>
public sealed record RouteRulerPalette(
    SKColor Map,
    SKColor Road,
    SKColor Park,
    SKColor Water,
    SKColor Ink,
    SKColor Ink2,
    SKColor Ink3,
    SKColor Accent,
    SKColor OnAccent,
    SKColor Amber,
    SKColor AmberInk,
    SKColor Danger,
    SKColor DangerInk,
    SKColor Pin,
    SKColor NodeStroke)
{
    /// <summary>Light-theme values, used until the control has read the live resources.</summary>
    public static RouteRulerPalette Fallback { get; } = new(
        Map: new SKColor(0xEC, 0xEF, 0xEC),
        Road: SKColors.White,
        Park: new SKColor(0xD6, 0xE7, 0xCF),
        Water: new SKColor(0xBC, 0xD7, 0xF2),
        Ink: new SKColor(0x1B, 0x24, 0x30),
        Ink2: new SKColor(0x5B, 0x65, 0x70),
        Ink3: new SKColor(0x98, 0xA1, 0xAB),
        Accent: new SKColor(0x21, 0x69, 0xE9),
        OnAccent: SKColors.White,
        Amber: new SKColor(0xE8, 0x93, 0x1C),
        AmberInk: new SKColor(0xB8, 0x69, 0x0A),
        Danger: new SKColor(0xD9, 0x45, 0x3A),
        DangerInk: new SKColor(0xB7, 0x35, 0x2C),
        Pin: new SKColor(0x3A, 0x42, 0x4B),
        NodeStroke: SKColors.White);
}

