using SkiaSharp;

namespace MorningCard.Controls;

/// <summary>
/// Draws the slack ruler along the route: minutes become distance on the path, so the wait before
/// leaving is as visible as the drive itself.
///
/// No XAML types here, and nothing is allocated per frame. Paints, the path and its measure are
/// built once and reused; the render thread only reads them.
/// </summary>
internal sealed class RouteRulerRenderer : IDisposable
{
    // The design space the mockup was drawn in. Everything below is in these units.
    private const float DesignWidth = 356f;
    private const float DesignHeight = 214f;

    private const string RouteData =
        "M34 186 L102 180 Q140 178 164 160 L200 130 Q222 112 244 88 L258 74 Q268 62 276 56";

    private const float SegmentWidth = 5f;
    private const float SlackDotRadius = 2.1f;
    private const float SlackDotStep = 7f;
    private const float LabelSize = 11.5f;
    private const float PillTextSize = 12f;
    private const float PillWidth = 58f;
    private const float PillHeight = 24f;

    /// <summary>
    /// Below this gap between the car and the leave node there is no room for a label above the
    /// path, so it drops underneath and the slack caption is dropped entirely.
    /// </summary>
    private const float LeaveLabelGapThreshold = 84f;

    private readonly SKPath _route = SKPath.ParseSvgPathData(RouteData);
    private readonly SKPathMeasure _measure;
    private readonly float _length;

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _segment = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = SegmentWidth,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly SKFont _labelFont = new() { Size = LabelSize, Edging = SKFontEdging.SubpixelAntialias };
    private readonly SKFont _pillFont = new() { Size = PillTextSize, Edging = SKFontEdging.SubpixelAntialias };
    private readonly SKPath _scratch = new();

    public RouteRulerRenderer()
    {
        _measure = new SKPathMeasure(_route, forceClosed: false);
        _length = _measure.Length;
    }

    /// <summary>Applies the packaged typeface once it has loaded; the default face is used until then.</summary>
    public void SetTypeface(SKTypeface typeface)
    {
        _labelFont.Typeface = typeface;
        _pillFont.Typeface = typeface;
    }

    public void Render(SKCanvas canvas, SKSize area, RouteRulerSnapshot snapshot)
    {
        var palette = snapshot.Palette;

        canvas.Save();

        // The panel slices the design space rather than letter-boxing it, matching the mockup's
        // xMidYMid slice: the map runs to the edges and the route keeps its proportions.
        var scale = Math.Max(area.Width / DesignWidth, area.Height / DesignHeight);
        canvas.Translate(
            (area.Width - (DesignWidth * scale)) / 2f,
            (area.Height - (DesignHeight * scale)) / 2f);
        canvas.Scale(scale);

        DrawGround(canvas, palette);

        if (snapshot.HasRoute)
        {
            DrawRoute(canvas, snapshot, palette);
        }

        canvas.Restore();
    }

    // ---- ground ------------------------------------------------------------------------------

    private void DrawGround(SKCanvas canvas, RouteRulerPalette palette)
    {
        canvas.Clear(palette.Map);

        _fill.Color = palette.Park;
        DrawSvg(canvas, "M120 40 q30 -18 62 2 q10 30 -20 44 q-40 6 -50 -14 q-6 -18 8 -32z", _fill);
        DrawSvg(canvas, "M300 20 q36 -14 52 20 q2 26 -28 30 q-30 -4 -24 -50z", _fill);
        DrawSvg(canvas, "M186 168 q26 -10 40 8 q6 20 -14 26 q-30 2 -26 -34z", _fill);

        _stroke.Color = palette.Water;
        _stroke.StrokeWidth = 9f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        _stroke.StrokeJoin = SKStrokeJoin.Round;
        DrawSvg(canvas, "M356 120 q-30 12 -50 44 q-14 24 -46 40 q-20 8 -30 30", _stroke);

        // A street grid at a slight angle, so the route reads as running across a neighbourhood
        // rather than along a ruler.
        canvas.Save();
        canvas.RotateDegrees(-22f, 178f, 107f);

        _stroke.Color = palette.Road;
        _stroke.StrokeWidth = 1.6f;
        DrawGrid(canvas, major: true);

        _stroke.Color = palette.Road.WithAlpha(204);
        _stroke.StrokeWidth = 0.9f;
        DrawGrid(canvas, major: false);

        canvas.Restore();

        // The left third fades to the map colour so the ETA block always has a clean ground.
        using var fade = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(DesignWidth * 0.55f, 0),
            new[] { palette.Map.WithAlpha(235), palette.Map.WithAlpha(0) },
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp);

        _fill.Shader = fade;
        _fill.Color = SKColors.White;
        canvas.DrawRect(0, 0, DesignWidth, DesignHeight, _fill);
        _fill.Shader = null;
    }

    private void DrawGrid(SKCanvas canvas, bool major)
    {
        var spacing = major ? 40f : 40f;
        var offset = major ? -20f : 0f;

        for (var y = offset; y <= 260f; y += spacing)
        {
            canvas.DrawLine(-40f, y, 420f, y, _stroke);
        }

        var xSpacing = major ? 50f : 50f;
        var xOffset = major ? 0f : 25f;

        for (var x = xOffset; x <= 400f; x += xSpacing)
        {
            canvas.DrawLine(x, -80f, x, 320f, _stroke);
        }
    }

    private void DrawSvg(SKCanvas canvas, string data, SKPaint paint)
    {
        _scratch.Reset();
        using var path = SKPath.ParseSvgPathData(data);
        canvas.DrawPath(path, paint);
    }

    // ---- the ruler ---------------------------------------------------------------------------

    private void DrawRoute(SKCanvas canvas, RouteRulerSnapshot snapshot, RouteRulerPalette palette)
    {
        var late = snapshot.IsLate;
        var driveColor = late ? palette.Danger : palette.Accent;
        var pointInk = late ? palette.DangerInk : palette.Ink;

        var fLeave = (float)Math.Clamp(snapshot.Leave, 0d, 1d);
        var fArrive = (float)Math.Clamp(snapshot.Arrive, 0d, 1d);

        var p0 = PointAt(0f);
        var pLeave = PointAt(fLeave);
        var pArrive = PointAt(fArrive);
        var pBell = PointAt(1f);

        var t = snapshot.Progress;
        var hasSlack = !late && fLeave > 0f;

        // Trip order: the dots fill in, the drive draws, the walk follows, then the markers land.
        var dotsT = Ease(Phase(t, 0.00, 0.18));
        var driveT = Ease(Phase(t, 0.14, 0.52));
        var walkT = Ease(Phase(t, 0.50, 0.66));
        var markerT = Phase(t, 0.62, 0.82);
        var labelT = Phase(t, 0.78, 1.00);

        if (hasSlack && dotsT > 0f)
        {
            DrawSlackDots(canvas, fLeave, dotsT, palette.Ink3);
        }

        if (driveT > 0f)
        {
            _segment.Color = driveColor;
            DrawSegment(canvas, late ? 0f : fLeave, fArrive, driveT);
        }

        if (walkT > 0f)
        {
            _segment.Color = palette.Amber;
            DrawSegment(canvas, fArrive, 1f, walkT);
        }

        // The car sits at the start whether or not there is slack left.
        _fill.Color = palette.Accent.WithAlpha(46);
        canvas.DrawCircle(p0.X, p0.Y, 14f, _fill);
        DrawNode(canvas, p0, 6.5f, palette.Accent, palette.NodeStroke, 3f);

        if (markerT > 0f)
        {
            var pop = BackOut(markerT);

            if (hasSlack)
            {
                DrawPoppedNode(canvas, pLeave, 5f, palette.Accent, palette.NodeStroke, 2.5f, pop);
            }

            DrawPoppedNode(canvas, pArrive, 5f, late ? palette.Danger : palette.Accent, palette.NodeStroke, 2.5f, pop);
            DrawPoppedNode(canvas, pBell, 4f, palette.Amber, palette.NodeStroke, 2.5f, pop);

            DrawPin(canvas, pBell, palette, pop);
            DrawDrivePill(canvas, snapshot, fLeave, fArrive, late, palette, pop);
        }

        if (labelT > 0f)
        {
            DrawLabels(canvas, snapshot, p0, pLeave, pArrive, pBell, fLeave, fArrive, hasSlack, pointInk, palette, labelT);
        }
    }

    private void DrawSlackDots(SKCanvas canvas, float fLeave, float progress, SKColor color)
    {
        _fill.Color = color;

        var from = 12f;
        var to = (fLeave * _length) - 10f;

        if (to <= from)
        {
            return;
        }

        var count = (int)Math.Floor((to - from) / SlackDotStep) + 1;
        var visible = (int)Math.Ceiling(count * progress);

        for (var i = 0; i < visible; i++)
        {
            var distance = from + (i * SlackDotStep);

            if (distance >= to)
            {
                break;
            }

            if (_measure.GetPosition(distance, out var point))
            {
                canvas.DrawCircle(point.X, point.Y, SlackDotRadius, _fill);
            }
        }
    }

    /// <summary>Draws the stretch between two fractions, grown to <paramref name="progress"/>.</summary>
    private void DrawSegment(SKCanvas canvas, float from, float to, float progress)
    {
        var start = from * _length;
        var end = start + ((to - from) * _length * progress);

        if (end - start < 0.5f)
        {
            return;
        }

        _scratch.Reset();

        if (_measure.GetSegment(start, end, _scratch, startWithMoveTo: true))
        {
            canvas.DrawPath(_scratch, _segment);
        }
    }

    private void DrawNode(SKCanvas canvas, SKPoint at, float radius, SKColor fill, SKColor ring, float ringWidth)
    {
        _fill.Color = fill;
        canvas.DrawCircle(at.X, at.Y, radius, _fill);

        _stroke.Color = ring;
        _stroke.StrokeWidth = ringWidth;
        canvas.DrawCircle(at.X, at.Y, radius, _stroke);
    }

    private void DrawPoppedNode(SKCanvas canvas, SKPoint at, float radius, SKColor fill, SKColor ring, float ringWidth, float pop)
    {
        canvas.Save();
        canvas.Translate(at.X, at.Y);
        canvas.Scale(pop);
        canvas.Translate(-at.X, -at.Y);
        DrawNode(canvas, at, radius, fill, ring, ringWidth);
        canvas.Restore();
    }

    private void DrawPin(SKCanvas canvas, SKPoint at, RouteRulerPalette palette, float pop)
    {
        canvas.Save();
        canvas.Translate(at.X, at.Y);
        canvas.Scale(pop);
        canvas.Translate(-at.X, -at.Y);

        _stroke.Color = palette.Ink3;
        _stroke.StrokeWidth = 1f;
        canvas.DrawLine(at.X, at.Y, at.X, at.Y - 18f, _stroke);

        _fill.Color = palette.Pin;
        using (var teardrop = SKPath.ParseSvgPathData(
            $"M{F(at.X)} {F(at.Y - 12f)} l-5 -8 a12 12 0 1 1 10 0 z"))
        {
            canvas.DrawPath(teardrop, _fill);
        }

        // The school mortarboard inside the pin.
        _fill.Color = palette.Map;
        using (var glyph = SKPath.ParseSvgPathData(
            "M12 3 2 8l10 5 8-4v6h2V8L12 3zm-6 9.5V17c0 1.7 2.7 3 6 3s6-1.3 6-3v-4.5l-6 3-6-3z"))
        {
            canvas.Save();
            canvas.Translate(at.X - 6f, at.Y - 40f);
            canvas.Scale(0.5f);
            canvas.DrawPath(glyph, _fill);
            canvas.Restore();
        }

        canvas.Restore();
    }

    private void DrawDrivePill(
        SKCanvas canvas,
        RouteRulerSnapshot snapshot,
        float fLeave,
        float fArrive,
        bool late,
        RouteRulerPalette palette,
        float pop)
    {
        var mid = PointAt((fLeave + fArrive) / 2f);

        // The pill sits below and right of the drive's midpoint, in the open corner of the map.
        var cx = mid.X + 30f;
        var cy = mid.Y + 32f;

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.Scale(pop);
        canvas.Translate(-cx, -cy);

        _fill.Color = late ? palette.Danger : palette.Accent;
        var rect = new SKRect(cx - (PillWidth / 2f), cy - (PillHeight / 2f), cx + (PillWidth / 2f), cy + (PillHeight / 2f));
        canvas.DrawRoundRect(rect, 7f, 7f, _fill);

        _fill.Color = palette.OnAccent;
        var width = _pillFont.MeasureText(snapshot.DriveLabel);
        canvas.DrawText(snapshot.DriveLabel, cx - (width / 2f), cy + 4.5f, _pillFont, _fill);

        canvas.Restore();
    }

    private void DrawLabels(
        SKCanvas canvas,
        RouteRulerSnapshot snapshot,
        SKPoint p0,
        SKPoint pLeave,
        SKPoint pArrive,
        SKPoint pBell,
        float fLeave,
        float fArrive,
        bool hasSlack,
        SKColor pointInk,
        RouteRulerPalette palette,
        float alpha)
    {
        var a = (byte)Math.Clamp(alpha * 255f, 0f, 255f);

        Label(canvas, snapshot.NowLabel, p0.X + 2f, p0.Y - 16f, TextAnchor.Start, pointInk.WithAlpha(a), palette.Map);

        if (hasSlack)
        {
            var gap = pLeave.X - p0.X;

            if (gap > LeaveLabelGapThreshold)
            {
                Label(canvas, snapshot.LeaveLabel, pLeave.X - 4f, pLeave.Y - 16f, TextAnchor.Start, palette.Ink.WithAlpha(a), palette.Map);

                var midSlack = PointAt(fLeave / 2f);
                Label(canvas, snapshot.SlackLabel, midSlack.X, midSlack.Y + 20f, TextAnchor.Middle, palette.Ink2.WithAlpha(a), palette.Map);
            }
            else
            {
                // Too tight for a label above the path, and the slack caption would collide.
                Label(canvas, snapshot.LeaveLabel, pLeave.X + 6f, pLeave.Y + 22f, TextAnchor.Start, palette.Ink.WithAlpha(a), palette.Map);
            }
        }

        Label(canvas, snapshot.ArriveLabel, pArrive.X - 10f, pArrive.Y - 6f, TextAnchor.End, pointInk.WithAlpha(a), palette.Map);

        var midWalk = PointAt((fArrive + 1f) / 2f);
        Label(canvas, snapshot.WalkLabel, midWalk.X + 6f, midWalk.Y + 22f, TextAnchor.Start, palette.AmberInk.WithAlpha(a), palette.Map);

        Label(canvas, snapshot.BellLabel, pBell.X + 11f, pBell.Y + 4f, TextAnchor.Start, pointInk.WithAlpha(a), palette.Map);
    }

    private enum TextAnchor
    {
        Start,
        Middle,
        End,
    }

    /// <summary>
    /// Text with a halo in the map colour, so a label stays legible where it crosses a road or the
    /// route itself.
    /// </summary>
    private void Label(SKCanvas canvas, string text, float x, float y, TextAnchor anchor, SKColor color, SKColor halo)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var width = _labelFont.MeasureText(text);

        var left = anchor switch
        {
            TextAnchor.Middle => x - (width / 2f),
            TextAnchor.End => x - width,
            _ => x,
        };

        _stroke.Color = halo.WithAlpha(color.Alpha);
        _stroke.StrokeWidth = 3f;
        _stroke.StrokeJoin = SKStrokeJoin.Round;
        canvas.DrawText(text, left, y, _labelFont, _stroke);

        _fill.Color = color;
        canvas.DrawText(text, left, y, _labelFont, _fill);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private SKPoint PointAt(float fraction) =>
        _measure.GetPosition(Math.Clamp(fraction, 0f, 1f) * _length, out var point)
            ? point
            : SKPoint.Empty;

    /// <summary>Maps overall progress onto one phase of the entrance, clamped to 0..1.</summary>
    private static float Phase(double t, double from, double to) =>
        (float)Math.Clamp((t - from) / (to - from), 0d, 1d);

    /// <summary>cubic-bezier(.25,1,.5,1), the house ease-out.</summary>
    private static float Ease(float t) => 1f - (float)Math.Pow(1d - t, 3d);

    /// <summary>
    /// cubic-bezier(.34,1.4,.64,1) evaluated directly. A KeySpline cannot express it: its control
    /// points are clamped to 0..1 and this one overshoots to about 1.05.
    /// </summary>
    private static float BackOut(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        var u = t - 1f;
        return 1f + (c3 * u * u * u) + (c1 * u * u);
    }

    private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        _route.Dispose();
        _measure.Dispose();
        _scratch.Dispose();
        _fill.Dispose();
        _stroke.Dispose();
        _segment.Dispose();
        _labelFont.Dispose();
        _pillFont.Dispose();
    }
}
