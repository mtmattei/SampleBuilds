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
// xaml-lint: allow builtin - ShadowContainer paints under a translucent shell and tints it (rules file); this draws the v6 ring and two shadows outside it
public sealed partial class CardShadowCanvas : SKCanvasElement
{
    /// <summary>How far the element extends past the card on every side, in effective pixels.</summary>
    public const double Bleed = 60d;

    // v6: 0.5 px ring, 0 2 6, and 0 32 64 -24 (colours in CardTokens).
    private const float FarOffsetY = 32f;
    private const float FarBlur = 64f;
    private const float FarSpread = -24f;
    private const float NearOffsetY = 2f;
    private const float NearBlur = 6f;

    private readonly SKPaint _paint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _ringPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private SKColor _ring = new(0x2A, 0x2C, 0x2F, 0x1A);

    // Snapshotted on the UI thread. RenderOverride runs on the render thread, where reading
    // walking Application.Current.Resources is a cross-thread touch of the XAML
    // object graph - which is exactly what the sibling ruler canvas avoids and documents.
    private SKColor _far = new(0x14, 0x1C, 0x28, 0x73);
    private SKColor _near = new(0x14, 0x1C, 0x28, 0x14);
    private float _radius = 34f;

    private readonly SKRoundRect _hole = new();
    private readonly Dictionary<float, SKMaskFilter> _blurs = new();

    public CardShadowCanvas()
    {
        Margin = new Thickness(-Bleed);
        IsHitTestVisible = false;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CardChrome.Changed += OnChromeChanged;
        Snapshot();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        // The paint is not disposed here: a re-parent fires Unloaded then Loaded, and RenderOverride
        // would then be using a freed native handle. It lives as long as the element.
        CardChrome.Changed -= OnChromeChanged;

    private void OnChromeChanged(object? sender, EventArgs e) => Snapshot();

    private void Snapshot()
    {
        _far = ReadColor("CardShadowFarColor", _far);
        _near = ReadColor("CardShadowNearColor", _near);
        _ring = ReadColor("CardShadowRingColor", _ring);
        _radius = (float)CardChrome.CornerRadius;
        Invalidate();
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var bleed = (float)Bleed;
        var radius = _radius;

        var card = new SKRect(
            bleed,
            bleed,
            (float)area.Width - bleed,
            (float)area.Height - bleed);

        if (card.Width <= 0 || card.Height <= 0)
        {
            return;
        }

        var far = _far;
        var near = _near;

        canvas.Save();

        // Everything inside the card is the card's business: without this the wide layer tints the
        // glass from underneath.
        _hole.SetRect(card, radius, radius);
        canvas.ClipRoundRect(_hole, SKClipOperation.Difference, antialias: true);

        DrawLayer(canvas, card, radius, far, FarOffsetY, FarBlur, FarSpread);
        DrawLayer(canvas, card, radius, near, NearOffsetY, NearBlur, spread: 0f);

        // The hairline ring, drawn just outside the card edge so the shell's own fill covers nothing.
        _ringPaint.Color = _ring;
        canvas.DrawRoundRect(card, radius, radius, _ringPaint);

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

        // A CSS blur radius is about twice the Gaussian sigma. Cached per layer: these were two
        // native allocations every frame.
        var sigma = blur / 2f;

        if (!_blurs.TryGetValue(sigma, out var mask))
        {
            mask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
            _blurs[sigma] = mask;
        }

        _paint.MaskFilter = mask;

        var layerRadius = Math.Max(0f, radius + spread);
        canvas.DrawRoundRect(rect, layerRadius, layerRadius, _paint);

        _paint.MaskFilter = null;
    }

    private SKColor ReadColor(string key, SKColor fallback) =>
        CardPalette.Lookup(key) is Windows.UI.Color color
            ? new SKColor(color.R, color.G, color.B, color.A)
            : fallback;
}


