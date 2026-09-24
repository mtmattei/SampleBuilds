using MorningCard.Themes;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>
/// The card's one soft ambient shadow, with the card's own shape clipped out.
///
/// ShadowContainer draws each shadow as a full blurred round rect, clips to the content, then paints
/// the background over it, so a shadow ends up under the surface as well as around it. Clipping the
/// card's rect out with SKClipOperation.Difference keeps it strictly outside, the way a CSS
/// box-shadow behaves.
///
/// Drawing is clipped to the element, so this sits at a negative margin to leave room for the blur.
/// </summary>
public sealed partial class CardShadowCanvas : SKCanvasElement
{
    /// <summary>How far the element extends past the card on every side, in effective pixels.</summary>
    public const double Bleed = 48d;

    // 0 8px 32px -8px: low, wide and faint. It lifts the frame off the ground without reading as
    // a drop shadow, which is what keeps the surface matte.
    private const float OffsetY = 8f;
    private const float Blur = 32f;
    private const float Spread = -8f;

    private readonly SKPaint _paint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Snapshotted on the UI thread. RenderOverride runs on the render thread, where reading
    // ActualTheme or walking Application.Current.Resources is a cross-thread touch of the XAML
    // object graph - which is exactly what the sibling ruler canvas avoids and documents.
    private SKColor _color = new(0x2A, 0x24, 0x19, 0x1C);
    private float _radius = 28f;

    private readonly SKRoundRect _hole = new();

    // A CSS blur radius is about twice the Gaussian sigma. Built once: this was a native allocation
    // per frame.
    private readonly SKMaskFilter _blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Blur / 2f);

    public CardShadowCanvas()
    {
        Margin = new Thickness(-Bleed);
        IsHitTestVisible = false;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => Snapshot();
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
        _color = AccentPalette.Lookup("CardShadowColor", ActualTheme) is Windows.UI.Color color
            ? new SKColor(color.R, color.G, color.B, color.A)
            : _color;
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

        var shadow = card;
        shadow.Inflate(Spread, Spread);
        shadow.Offset(0f, OffsetY);

        if (shadow.Width <= 0 || shadow.Height <= 0)
        {
            return;
        }

        canvas.Save();

        _hole.SetRect(card, radius, radius);
        canvas.ClipRoundRect(_hole, SKClipOperation.Difference, antialias: true);

        _paint.Color = _color;
        _paint.MaskFilter = _blur;

        var shadowRadius = Math.Max(0f, radius + Spread);
        canvas.DrawRoundRect(shadow, shadowRadius, shadowRadius, _paint);

        _paint.MaskFilter = null;
        canvas.Restore();
    }
}
