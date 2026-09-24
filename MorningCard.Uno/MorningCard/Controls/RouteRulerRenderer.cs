using MorningCard.Core.Route;
using SkiaSharp;

namespace MorningCard.Controls;

/// <summary>
/// Draws the time ruler along the real route.
///
/// The route line is the ruler: ticks are placed by time, so the gap between two of them is how far
/// the car gets in that minute. They bunch where the drive is slow and spread where it is fast.
/// Slack has duration but no distance, so it is drawn as an arc around the pin where the waiting
/// happens rather than as a length of road.
///
/// Everything arrives already projected into screen pixels (see <see cref="RulerProjection"/>). No
/// XAML types here, no Mapsui, and nothing allocated per frame: paths are rebuilt only when the
/// projection itself changes, and the paints and the map filter are built once.
///
/// The drawing is static. There is no entrance: the route, ticks, pins and labels are simply there,
/// which is the matte design's rule that only press and hover move.
/// </summary>
internal sealed class RouteRulerRenderer : IDisposable
{
    // ---- measurements, all in device-independent pixels --------------------------------------

    /// <summary>The route line itself.</summary>
    private const float RouteWidth = 5f;

    /// <summary>A lighter casing under the line, so it reads against the street grid.</summary>
    private const float CasingWidth = 11.5f;

    /// <summary>The solid outline either side of the drive's pale fill.</summary>
    private const float DriveEdgeWidth = 1.5f;

    private const float MinorTickWidth = 1f;
    private const float MinorTickLength = 6f;
    private const float MajorTickWidth = 1.5f;
    private const float MajorTickLength = 10f;

    /// <summary>How far a major tick's label sits beyond the end of its tick.</summary>
    private const float TickLabelGap = 4f;

    private const float LabelSize = 10.5f;
    private const float CalloutTextSize = 12f;

    /// <summary>
    /// Screen-constant, so the ring reads as a clock face rather than as a piece of the map. A
    /// stationary car goes around the clock, not down the road.
    /// </summary>
    private const float RingRadius = 22f;

    private const float RingWidth = 2f;

    /// <summary>A ring is a dotted arc: this is the on/off run of the dash.</summary>
    private static readonly float[] RingDash = [2f, 4f];

    /// <summary>The walk is drawn hollow, so the change of mode reads without a legend.</summary>
    private static readonly float[] WalkDash = [1.5f, 5f];

    private const float PinRadius = 5.5f;
    private const float PinRingWidth = 2.5f;
    private const float CursorRadius = 5f;

    /// <summary>Callouts are status labels, so they share the chips' 7.</summary>
    private const float CalloutCornerRadius = 8f;

    private const float CalloutPadX = 7f;
    private const float CalloutPadY = 4f;

    /// <summary>How close two pins may be before only the more important one is labelled.</summary>
    private const float CalloutClearance = 46f;

    /// <summary>How near the panel edge a callout may sit before it is pushed back inside.</summary>
    private const float CalloutMargin = 6f;

    // ---- the map's colour --------------------------------------------------------------------

    /// <summary>How much of the tiles' own saturation survives. 0 is grey, 1 is OSM as served.</summary>
    private const float MapSaturation = 0.25f;

    /// <summary>How far the result is pulled toward <see cref="MapPaper"/>, lifting it to stone.</summary>
    private const float MapWash = 0.3f;

    /// <summary>Pale stone. Warm, so the washed streets sit in the same paper as the card.</summary>
    private static readonly SKColor MapPaper = new(0xEE, 0xEC, 0xE6);

    // ---- reused drawing state ----------------------------------------------------------------

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    private readonly SKPaint _line = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly SKFont _labelFont = new() { Size = LabelSize, Edging = SKFontEdging.SubpixelAntialias };
    private readonly SKFont _calloutFont = new() { Size = CalloutTextSize, Edging = SKFontEdging.SubpixelAntialias };

    private readonly SKPath _drive = new();
    private readonly SKPath _walk = new();
    private readonly SKRoundRect _panel = new();

    private readonly SKPathEffect _ringDash = SKPathEffect.CreateDash(RingDash, 0f);
    private readonly SKPathEffect _walkDash = SKPathEffect.CreateDash(WalkDash, 0f);

    /// <summary>
    /// Takes the tiles close to monochrome in place. The canvas draws on the window's own surface,
    /// so a backdrop filter samples the map underneath it: the tile source stays OSM, only its
    /// colour changes, and the route drawn afterwards is the one saturated mark on the panel.
    /// </summary>
    private readonly SKImageFilter _mapTone = SKImageFilter.CreateColorFilter(
        SKColorFilter.CreateColorMatrix(MapToneMatrix()));

    /// <summary>
    /// The projection the cached paths were built from. Compared by reference: a new projection is
    /// a new object.
    /// </summary>
    private RulerProjection? _builtFor;

    public void SetTypeface(SKTypeface typeface)
    {
        _labelFont.Typeface = typeface;
        _calloutFont.Typeface = typeface;
    }

    public void Render(SKCanvas canvas, SKSize area, RouteRulerSnapshot snapshot)
    {
        // A zero-sized first layout would feed NaN geometry to Skia, which is a native crash rather
        // than an exception. Nothing to draw at this size anyway.
        if (area.Width <= 0f || area.Height <= 0f)
        {
            return;
        }

        // First, and whether or not there is a route: the map is quiet in every state.
        ToneMap(canvas, area, snapshot.MapCornerRadius);

        if (!snapshot.HasRoute || snapshot.Projection is not { } projection)
        {
            return;
        }

        EnsurePaths(projection);

        var palette = snapshot.Palette;
        var driveColor = snapshot.IsLate ? palette.Danger : snapshot.IsLeaveNow ? palette.Amber : palette.Accent;
        var driveFill = snapshot.IsLate ? palette.LateFill : snapshot.IsLeaveNow ? palette.LeaveNowFill : palette.DriveFill;

        DrawCasing(canvas, palette);
        DrawRouteLines(canvas, palette, driveColor, driveFill);
        DrawTicks(canvas, projection, palette, driveColor);
        DrawRing(canvas, projection.OriginDwell, palette, driveColor);
        DrawRing(canvas, projection.DestinationDwell, palette, palette.Amber);
        DrawMarkers(canvas, projection, palette, driveColor);
        DrawCallouts(canvas, snapshot, projection, palette, area);
        DrawCursor(canvas, projection, palette, driveColor);
    }

    // ---- the map ------------------------------------------------------------------------------

    /// <summary>
    /// Re-colours whatever is already drawn under the panel, clipped to the panel's rounded shape.
    /// </summary>
    private void ToneMap(SKCanvas canvas, SKSize area, float radius)
    {
        var bounds = SKRect.Create(area.Width, area.Height);
        _panel.SetRect(bounds, radius, radius);

        canvas.Save();
        canvas.ClipRoundRect(_panel, SKClipOperation.Intersect, antialias: true);

        var layer = new SKCanvasSaveLayerRec { Bounds = bounds, Backdrop = _mapTone };
        canvas.SaveLayer(in layer);
        canvas.Restore();

        canvas.Restore();
    }

    /// <summary>
    /// Saturation cut to <see cref="MapSaturation"/> around Rec. 709 luminance, then a
    /// <see cref="MapWash"/> blend toward <see cref="MapPaper"/>. Greens become barely there, the
    /// river keeps a trace of grey-blue, and the streets go to pale stone.
    /// </summary>
    private static float[] MapToneMatrix()
    {
        const float lr = 0.2126f;
        const float lg = 0.7152f;
        const float lb = 0.0722f;
        const float s = MapSaturation;
        const float keep = 1f - MapWash;

        float[,] m =
        {
            { (lr * (1f - s)) + s, lg * (1f - s), lb * (1f - s) },
            { lr * (1f - s), (lg * (1f - s)) + s, lb * (1f - s) },
            { lr * (1f - s), lg * (1f - s), (lb * (1f - s)) + s },
        };

        // Translation is normalised to 0..1 in this SkiaSharp.
        var paper = new[] { MapPaper.Red / 255f, MapPaper.Green / 255f, MapPaper.Blue / 255f };
        var matrix = new float[20];

        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                matrix[(row * 5) + col] = m[row, col] * keep;
            }

            matrix[(row * 5) + 4] = paper[row] * MapWash;
        }

        matrix[18] = 1f;

        return matrix;
    }

    // ---- the route ----------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the cached paths when the projection changes - a new viewport, new times, or a
    /// theme switch. Not per frame.
    /// </summary>
    private void EnsurePaths(RulerProjection projection)
    {
        if (ReferenceEquals(_builtFor, projection))
        {
            return;
        }

        _builtFor = projection;

        BuildPath(_drive, projection.Drive);
        BuildPath(_walk, projection.Walk);
    }

    private static void BuildPath(SKPath path, IReadOnlyList<SKPoint> points)
    {
        path.Reset();

        if (points.Count < 2)
        {
            return;
        }

        path.MoveTo(points[0]);

        for (var i = 1; i < points.Count; i++)
        {
            path.LineTo(points[i]);
        }
    }

    /// <summary>A pale casing under the route, so it separates from the street it runs along.</summary>
    private void DrawCasing(SKCanvas canvas, RouteRulerPalette palette)
    {
        _line.PathEffect = null;
        _line.StrokeWidth = CasingWidth;
        _line.Color = palette.NodeStroke.WithAlpha(210);

        canvas.DrawPath(_drive, _line);
        canvas.DrawPath(_walk, _line);
    }

    /// <summary>
    /// The drive is the ruler's Drive block laid along the road: its pale fill, cased in the tone's
    /// solid colour so it still separates from the tiles.
    /// </summary>
    private void DrawRouteLines(SKCanvas canvas, RouteRulerPalette palette, SKColor driveColor, SKColor driveFill)
    {
        _line.PathEffect = null;
        _line.StrokeWidth = RouteWidth + 2f * DriveEdgeWidth;
        _line.Color = driveColor;
        canvas.DrawPath(_drive, _line);

        _line.StrokeWidth = RouteWidth;
        _line.Color = driveFill;
        canvas.DrawPath(_drive, _line);

        // Hollow, and clay: the mode change has to read without a legend.
        _line.PathEffect = _walkDash;
        _line.StrokeWidth = RouteWidth - 1f;
        _line.Color = palette.Amber;
        canvas.DrawPath(_walk, _line);
        _line.PathEffect = null;
    }

    // ---- ticks --------------------------------------------------------------------------------

    /// <summary>
    /// The minute marks, perpendicular to the route. Their spacing is the reading: bunched is slow,
    /// spread is fast.
    /// </summary>
    private void DrawTicks(
        SKCanvas canvas,
        RulerProjection projection,
        RouteRulerPalette palette,
        SKColor driveColor)
    {
        foreach (var tick in projection.Ticks)
        {
            var ink = tick.Mode == RouteMode.Walk ? palette.AmberInk : driveColor;

            var nx = (float)-Math.Sin(tick.BearingRadians);
            var ny = (float)Math.Cos(tick.BearingRadians);
            var half = (tick.IsMajor ? MajorTickLength : MinorTickLength) / 2f;

            _stroke.PathEffect = null;
            _stroke.Color = ink;
            _stroke.StrokeWidth = tick.IsMajor ? MajorTickWidth : MinorTickWidth;
            _stroke.StrokeCap = SKStrokeCap.Round;

            canvas.DrawLine(
                tick.Point.X - (nx * half),
                tick.Point.Y - (ny * half),
                tick.Point.X + (nx * half),
                tick.Point.Y + (ny * half),
                _stroke);

            if (!tick.IsMajor || string.IsNullOrEmpty(tick.Label))
            {
                continue;
            }

            // The label sits off the same side every time. Picking the "outside of the turn" per
            // tick would make consecutive labels flip sides on a winding street.
            var lx = tick.Point.X + (nx * (half + TickLabelGap));
            var ly = tick.Point.Y + (ny * (half + TickLabelGap)) + (LabelSize / 3f);

            Label(canvas, tick.Label, lx, ly, TextAnchor.Middle, ink, _labelFont);
        }
    }

    // ---- dwell rings --------------------------------------------------------------------------

    /// <summary>
    /// Time spent standing still, as an arc on a clock face: a full turn is sixty minutes and the
    /// arc starts at twelve.
    /// </summary>
    private void DrawRing(
        SKCanvas canvas,
        ProjectedRing? ring,
        RouteRulerPalette palette,
        SKColor color)
    {
        if (ring is not { } value || value.SweepDegrees <= 0f)
        {
            return;
        }

        var box = new SKRect(
            value.Center.X - RingRadius,
            value.Center.Y - RingRadius,
            value.Center.X + RingRadius,
            value.Center.Y + RingRadius);

        // A faint full circle behind the arc, so the hour the arc is a fraction of is visible.
        _stroke.PathEffect = null;
        _stroke.StrokeWidth = 1f;
        _stroke.StrokeCap = SKStrokeCap.Butt;
        _stroke.Color = palette.Ink3.WithAlpha(70);
        canvas.DrawCircle(value.Center, RingRadius, _stroke);

        // -90 starts the sweep at twelve o'clock; Skia measures from three.
        _stroke.PathEffect = _ringDash;
        _stroke.StrokeWidth = RingWidth;
        _stroke.StrokeCap = SKStrokeCap.Round;
        _stroke.Color = color;

        using var arc = new SKPath();
        arc.AddArc(box, -90f, value.SweepDegrees);
        canvas.DrawPath(arc, _stroke);

        _stroke.PathEffect = null;

        if (string.IsNullOrEmpty(value.OverflowLabel))
        {
            return;
        }

        // Past the hour the ring is already full, so the remainder goes in a badge.
        Label(
            canvas,
            value.OverflowLabel,
            value.Center.X,
            value.Center.Y - RingRadius - 6f,
            TextAnchor.Middle,
            color,
            _labelFont);
    }

    // ---- pins and cursor ----------------------------------------------------------------------

    private void DrawMarkers(
        SKCanvas canvas,
        RulerProjection projection,
        RouteRulerPalette palette,
        SKColor driveColor)
    {
        DrawPin(canvas, projection.Origin, driveColor, palette.NodeStroke);

        // The parking spot and the door are a couple of hundred metres apart, which at the zoom a
        // whole commute fits into is a few pixels - two pins touching, reading as one smudged
        // marker rather than as two places. The same clearance the callouts use decides it: below
        // that, only the door is marked, because that is the end of the trip.
        if (projection.HasWalk && Apart(projection.Parking, projection.Destination) >= CalloutClearance)
        {
            DrawPin(canvas, projection.Parking, palette.Amber, palette.NodeStroke);
        }

        DrawPin(canvas, projection.Destination, palette.Pin, palette.NodeStroke);
    }

    private void DrawPin(SKCanvas canvas, SKPoint at, SKColor fill, SKColor ring)
    {
        _stroke.PathEffect = null;
        _stroke.Color = ring;
        _stroke.StrokeWidth = PinRingWidth;
        _stroke.StrokeCap = SKStrokeCap.Butt;
        canvas.DrawCircle(at, PinRadius, _stroke);

        _fill.Color = fill;
        canvas.DrawCircle(at, PinRadius, _fill);
    }

    /// <summary>
    /// Where the morning has got to: a plain dot on the route, facing nothing and wearing nothing.
    /// The thin cut-out stroke only separates it from the line it sits on.
    /// </summary>
    private void DrawCursor(
        SKCanvas canvas,
        RulerProjection projection,
        RouteRulerPalette palette,
        SKColor driveColor)
    {
        var cursor = projection.Cursor;

        // Not on the origin ring. That ring shortens as the wait runs out rather than filling, so
        // the marker was pinned to its far end - which is the arc's own terminus, right beside the
        // origin pin. The arc ending where it ends is already the reading.
        if (cursor.Kind is NowCursorKind.None or NowCursorKind.OnOriginRing)
        {
            return;
        }

        var at = cursor.Point;

        if (cursor.Kind == NowCursorKind.OnDestinationRing)
        {
            var angle = (cursor.RingAngleDegrees - 90f) * Math.PI / 180d;

            at = new SKPoint(
                cursor.Point.X + (float)(Math.Cos(angle) * RingRadius),
                cursor.Point.Y + (float)(Math.Sin(angle) * RingRadius));
        }

        var color = cursor.Kind == NowCursorKind.OnDestinationRing ? palette.Amber : driveColor;

        _stroke.PathEffect = null;
        _stroke.Color = palette.NodeStroke;
        _stroke.StrokeWidth = 2f;
        _stroke.StrokeCap = SKStrokeCap.Butt;
        canvas.DrawCircle(at, CursorRadius, _stroke);

        _fill.Color = color;
        canvas.DrawCircle(at, CursorRadius, _fill);
    }

    // ---- callouts -----------------------------------------------------------------------------

    /// <summary>
    /// The departure, anchored to where it happens. The only plate on the map: the arrival and the
    /// bell are already in the timeline strip above, and a second copy here was repetition.
    /// </summary>
    private void DrawCallouts(
        SKCanvas canvas,
        RouteRulerSnapshot snapshot,
        RulerProjection projection,
        RouteRulerPalette palette,
        SKSize area)
    {
        var driveColor = snapshot.IsLate ? palette.Danger : snapshot.IsLeaveNow ? palette.Amber : palette.Accent;

        // A pin wearing a dwell ring is four times its own radius across, so its label has to clear
        // the ring rather than the pin - otherwise the arc is drawn straight through the text.
        Callout(
            canvas, snapshot.LeaveLabel, projection.Origin, driveColor, palette.OnAccent,
            area, Reach(projection.OriginDwell));
    }

    /// <summary>How far out from a pin its own furniture reaches: the ring when it has one.</summary>
    private static float Reach(ProjectedRing? ring) =>
        ring is { SweepDegrees: > 0f } ? RingRadius + (RingWidth / 2f) : PinRadius;

    private static float Apart(SKPoint a, SKPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;

        return (float)Math.Sqrt((dx * dx) + (dy * dy));
    }

    private void Callout(
        SKCanvas canvas,
        string text,
        SKPoint anchor,
        SKColor plate,
        SKColor ink,
        SKSize area,
        float reach)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var width = _calloutFont.MeasureText(text);
        var height = CalloutTextSize + (CalloutPadY * 2f);
        var offset = reach + 6f;

        var top = anchor.Y - offset - height;

        // A callout on a pin near the panel edge would otherwise be cut in half by it. Flipping to
        // the other side of its pin keeps it attached to the thing it names, where nudging it along
        // the edge would leave it pointing at nothing.
        if (top < CalloutMargin)
        {
            top = anchor.Y + offset;
        }
        else if (top + height > area.Height - CalloutMargin)
        {
            top = anchor.Y - offset - height;
        }

        var half = (width / 2f) + CalloutPadX;
        var centerX = Math.Clamp(anchor.X, half + CalloutMargin, area.Width - half - CalloutMargin);

        var box = new SKRect(
            centerX - half,
            top,
            centerX + half,
            top + height);

        _fill.Color = plate;
        canvas.DrawRoundRect(box, CalloutCornerRadius, CalloutCornerRadius, _fill);

        _fill.Color = ink;
        canvas.DrawText(
            text,
            box.MidX - (width / 2f),
            box.MidY + (CalloutTextSize / 3f),
            _calloutFont,
            _fill);
    }

    /// <summary>
    /// Plain text on the map. No halo: with the tiles toned down to stone, the ink carries on its
    /// own, and a halo was one more shadow on a surface meant to read as matte.
    /// </summary>
    private void Label(
        SKCanvas canvas,
        string text,
        float x,
        float y,
        TextAnchor anchor,
        SKColor color,
        SKFont font)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var width = font.MeasureText(text);

        var left = anchor switch
        {
            TextAnchor.Middle => x - (width / 2f),
            TextAnchor.End => x - width,
            _ => x,
        };

        _fill.Color = color;
        canvas.DrawText(text, left, y, font, _fill);
    }

    public void Dispose()
    {
        _drive.Dispose();
        _walk.Dispose();
        _panel.Dispose();
        _ringDash.Dispose();
        _walkDash.Dispose();
        _mapTone.Dispose();
        _fill.Dispose();
        _stroke.Dispose();
        _line.Dispose();
        _labelFont.Dispose();
        _calloutFont.Dispose();
    }
}

internal enum TextAnchor
{
    Start,
    Middle,
    End,
}
