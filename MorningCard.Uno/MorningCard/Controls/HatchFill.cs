using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>
/// A 135° hatch in a rounded block: the slack ruler's past, after and overrun blocks.
///
/// Skia rather than a repeating LinearGradientBrush. Spike R9 (2026-09-23) found the brush works on
/// both heads, but its hard stops are not antialiased, so a 1 px line at 6 px pitch steps thick and
/// thin along its length. Lines drawn as antialiased strokes keep one weight.
///
/// Everything is set once at construction and read on the render thread, so there is nothing to
/// snapshot: the owner builds a new block whenever the ruler's layout changes.
/// </summary>
internal sealed class HatchFill : SKCanvasElement
{
    private readonly SKPaint _ground;
    private readonly SKPaint _line;
    private readonly SKPaint? _ring;
    private readonly float _pitch;
    private readonly float _radius;

    public HatchFill(SKColor ground, SKColor ink, float strokeWidth, float pitch, float radius, SKColor? ring = null)
    {
        IsHitTestVisible = false;
        _pitch = pitch;
        _radius = radius;
        _ground = new SKPaint { Color = ground, IsAntialias = true, Style = SKPaintStyle.Fill };
        _line = new SKPaint { Color = ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = strokeWidth };

        if (ring is { } ringColor)
        {
            _ring = new SKPaint { Color = ringColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        }

        // No dispose on Unloaded: a re-parented element gets Unloaded then Loaded, and would render
        // with freed paints. Three small paints per block are left to the finalizer.
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var w = (float)area.Width;
        var h = (float)area.Height;

        if (w <= 0 || h <= 0)
        {
            return;
        }

        var shape = new SKRoundRect(new SKRect(0, 0, w, h), _radius);

        canvas.Save();
        canvas.ClipRoundRect(shape, antialias: true);
        canvas.DrawRoundRect(shape, _ground);

        // Lines x + y = c, `pitch` apart measured square to the lines, so pitch·√2 apart along x.
        var step = _pitch * MathF.Sqrt(2);
        for (var c = 0.5f; c < w + h; c += step)
        {
            canvas.DrawLine(c, 0, c - h, h, _line);
        }

        canvas.Restore();

        if (_ring is not null)
        {
            // Inset half a pixel so the 1 px ring sits inside the block, like a CSS inset shadow.
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(0.5f, 0.5f, w - 0.5f, h - 0.5f), Math.Max(0, _radius - 0.5f)), _ring);
        }
    }
}
