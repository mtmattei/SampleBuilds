using MorningCard.Themes;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>
/// The card's two shadow layers, with the card's own shape clipped out.
///
/// ShadowContainer was the first choice and cannot do this design: it draws each shadow as a full
/// blurred round rect, clips to the content, then paints the background over it. Under a 74 percent
/// alpha glass the wide layer therefore darkens the card's own interior, which a CSS box-shadow
/// never does. Clipping the card's rect out with SKClipOperation.Difference puts the light back
/// where it belongs. ShadowContainer remains the right control for an opaque surface.
///
/// Drawing is clipped to the element, so this sits at a negative margin to leave room for the blur.
/// </summary>
public sealed partial class CardShadowCanvas : SKCanvasElement
{
    /// <summary>How far the element extends past the card on every side, in effective pixels.</summary>
    public const double Bleed = 60d;

    // Ported from the prototype: 0 30 60 -30 at 45%, and 0 2 6 at 8%.
    private const float FarOffsetY = 30f;
    private const float FarBlur = 60f;
    private const float FarSpread = -30f;
    private const float NearOffsetY = 2f;
    private const float NearBlur = 6f;

    private readonly SKPaint _paint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    public CardShadowCanvas()
    {
        Margin = new Thickness(-Bleed);
        IsHitTestVisible = false;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => Invalidate();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CardChrome.Changed += OnChromeChanged;
        Invalidate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CardChrome.Changed -= OnChromeChanged;
        _paint.Dispose();
    }

    private void OnChromeChanged(object? sender, EventArgs e) => Invalidate();

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var bleed = (float)Bleed;
        var radius = (float)CardChrome.CornerRadius;

        var card = new SKRect(
            bleed,
            bleed,
            (float)area.Width - bleed,
            (float)area.Height - bleed);

        if (card.Width <= 0 || card.Height <= 0)
        {
            return;
        }

        var far = ReadColor("CardShadowFarColor", new SKColor(0x14, 0x1C, 0x28, 0x73));
        var near = ReadColor("CardShadowNearColor", new SKColor(0x14, 0x1C, 0x28, 0x14));

        canvas.Save();

        // Everything inside the card is the card's business: without this the wide layer tints the
        // glass from underneath.
        using (var hole = new SKRoundRect(card, radius, radius))
        {
            canvas.ClipRoundRect(hole, SKClipOperation.Difference, antialias: true);
        }

        DrawLayer(canvas, card, radius, far, FarOffsetY, FarBlur, FarSpread);
        DrawLayer(canvas, card, radius, near, NearOffsetY, NearBlur, spread: 0f);

        canvas.Restore();
    }

    private void DrawLayer(
        SKCanvas canvas,
        SKRect card,
        float radius,
        SKColor color,
        float offsetY,
        float blur,
        float spread)
    {
        var rect = card;
        rect.Inflate(spread, spread);
        rect.Offset(0f, offsetY);

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        _paint.Color = color;

        // A CSS blur radius is about twice the Gaussian sigma.
        using var mask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blur / 2f);
        _paint.MaskFilter = mask;

        var layerRadius = Math.Max(0f, radius + spread);
        canvas.DrawRoundRect(rect, layerRadius, layerRadius, _paint);

        _paint.MaskFilter = null;
    }

    private SKColor ReadColor(string key, SKColor fallback) =>
        AccentPalette.Lookup(key, ActualTheme) is Windows.UI.Color color
            ? new SKColor(color.R, color.G, color.B, color.A)
            : fallback;
}

