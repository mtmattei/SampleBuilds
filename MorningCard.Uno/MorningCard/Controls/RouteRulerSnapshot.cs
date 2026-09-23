using SkiaSharp;

namespace MorningCard.Controls;

/// <summary>
/// Everything the renderer needs, captured on the UI thread. RenderOverride runs on the render
/// thread, so it reads plain fields and never touches a DependencyProperty or a theme resource.
/// </summary>
public sealed record RouteRulerSnapshot
{
    public bool HasRoute { get; init; }

    /// <summary>
    /// The route in screen pixels, projected through the map's viewport on the UI thread. Null
    /// before the asset has loaded or the map has been fitted, which leaves only the tiles.
    /// </summary>
    public RulerProjection? Projection { get; init; }

    /// <summary>Minutes before the car has to leave; 0 or less puts the now dot on the leave point.</summary>
    public int SlackMin { get; init; }

    /// <summary>The now dot's colour: slate on time, brown to leave now, brick when late.</summary>
    public SKColor Tone { get; init; } = RouteRulerPalette.Fallback.Accent;

    /// <summary>"Now 7:24".</summary>
    public string NowLabel { get; init; } = string.Empty;

    /// <summary>"3 min walk".</summary>
    public string WalkLabel { get; init; } = string.Empty;

    /// <summary>0 to 1 across the whole entrance; 1 means settled.</summary>
    public double Progress { get; init; } = 1d;

    public RouteRulerPalette Palette { get; init; } = RouteRulerPalette.Fallback;
}

/// <summary>
/// The colours, snapshotted from the card tokens. The canvas draws straight onto the window
/// surface, so it cannot inherit brushes or opacity from XAML.
/// </summary>
/// <param name="Map">The paper casing under both legs.</param>
/// <param name="Accent">The drive leg and its rings (slate in every state).</param>
/// <param name="Amber">The walk leg.</param>
/// <param name="Pin">The school disc.</param>
/// <param name="NodeStroke">White: the pin ring, the now halo and the pin glyph.</param>
/// <param name="Surface">Paper: ring centres and the now callout.</param>
public sealed record RouteRulerPalette(
    SKColor Map,
    SKColor Accent,
    SKColor Amber,
    SKColor AmberInk,
    SKColor Pin,
    SKColor NodeStroke,
    SKColor Surface,
    SKColor SlackDot,
    SKColor CalloutInk,
    SKColor CalloutRing,
    SKColor WalkPlate)
{
    /// <summary>The v6 values, used until the control has read the live tokens.</summary>
    public static RouteRulerPalette Fallback { get; } = new(
        Map: new SKColor(0xEF, 0xEE, 0xE9),
        Accent: new SKColor(0x4A, 0x6B, 0x8A),
        Amber: new SKColor(0xB5, 0x82, 0x4F),
        AmberInk: new SKColor(0x8A, 0x62, 0x36),
        Pin: new SKColor(0x3A, 0x3C, 0x3F),
        NodeStroke: SKColors.White,
        Surface: new SKColor(0xFB, 0xFA, 0xF7),
        SlackDot: new SKColor(0xA8, 0xA4, 0x9C),
        CalloutInk: new SKColor(0x2B, 0x2D, 0x30),
        CalloutRing: new SKColor(0x2A, 0x2C, 0x2F, 0x14),
        WalkPlate: new SKColor(0xF0, 0xEA, 0xDF));
}
