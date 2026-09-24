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


    public string LeaveLabel { get; init; } = string.Empty;


    /// <summary>The map panel's corner, so the map's tone is clipped to the panel's shape.</summary>
    public float MapCornerRadius { get; init; } = 4f;

    public RouteRulerPalette Palette { get; init; } = RouteRulerPalette.Fallback;

}

/// <summary>
/// The colours, snapshotted from the app's theme resources. The canvas draws straight onto the
/// window surface, so it cannot inherit brushes or opacity from XAML.
/// </summary>
public sealed record RouteRulerPalette(
    SKColor Map,
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
        Map: new SKColor(0xEE, 0xEC, 0xE6),
        Ink3: new SKColor(0x6E, 0x78, 0x85),
        Accent: new SKColor(0x4A, 0x6B, 0x8A),
        OnAccent: SKColors.White,
        Amber: new SKColor(0xB5, 0x82, 0x4F),
        AmberInk: new SKColor(0x85, 0x59, 0x2D),
        Danger: new SKColor(0xA8, 0x5A, 0x45),
        DangerInk: new SKColor(0x8E, 0x45, 0x33),
        Pin: new SKColor(0x3D, 0x3A, 0x35),
        NodeStroke: SKColors.White);
}
