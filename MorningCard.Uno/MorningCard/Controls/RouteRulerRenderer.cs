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
/// projection itself changes, and the paints are built once.
/// </summary>
internal sealed class RouteRulerRenderer : IDisposable
{
    // ---- measurements, all in device-independent pixels --------------------------------------

    /// <summary>The route line itself.</summary>
    private const float RouteWidth = 5f;

    /// <summary>A lighter casing under the line, so it reads against a busy street grid.</summary>
    private const float CasingWidth = 8.5f;

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

    /// <summary>The app's shared small radius, rather than a ninth invented value.</summary>
    private const float CalloutCornerRadius = 8f;

    private const float CalloutPadX = 7f;
    private const float CalloutPadY = 4f;

    /// <summary>How close two pins may be before only the more important one is labelled.</summary>
    private const float CalloutClearance = 46f;

    /// <summary>How near the panel edge a callout may sit before it is pushed back inside.</summary>
    private const float CalloutMargin = 6f;

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

    private SKPathMeasure? _driveMeasure;
    private SKPathMeasure? _walkMeasure;
    private SKPathEffect? _ringDash;
    private SKPathEffect? _walkDash;

    /// <summary>
    /// The projection the cached paths were built from. Compared by reference: a new projection is
    /// a new object, and building a path per frame during the entrance would be thirty a second.
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
        if (area.Width <= 0f || area.Height <= 0f || !snapshot.HasRoute)
        {
            return;
        }

        if (snapshot.Projection is not { } projection)
        {
            return;
        }

        EnsurePaths(projection);

        var palette = snapshot.Palette;
        var t = snapshot.Progress;

        // Trip order: the wait appears, the drive draws itself along the road, the walk follows,
        // the ticks arrive, then the pins land and the labels settle.
        var ringT = Ease(Phase(t, 0.00, 0.20));
        var driveT = Ease(Phase(t, 0.14, 0.54));
        var walkT = Ease(Phase(t, 0.52, 0.68));
        var tickT = Phase(t, 0.62, 0.82);
        var markerT = Phase(t, 0.70, 0.90);
        var labelT = Phase(t, 0.82, 1.00);

        var driveColor = snapshot.IsLate ? palette.Danger : palette.Accent;

        DrawCasing(canvas, palette, driveT, walkT);
        DrawRouteLines(canvas, palette, driveColor, driveT, walkT);

        if (tickT > 0f)
        {
            DrawTicks(canvas, projection, palette, driveColor, tickT);
        }

        if (ringT > 0f)
        {
            DrawRing(canvas, projection.OriginDwell, palette, driveColor, ringT);
            DrawRing(canvas, projection.DestinationDwell, palette, palette.Amber, ringT);
        }

        if (markerT > 0f)
        {
            DrawMarkers(canvas, projection, palette, driveColor, BackOut(markerT));
        }

        if (labelT > 0f)
        {
            DrawCallouts(canvas, snapshot, projection, palette, labelT, area);
        }

        DrawCursor(canvas, projection, palette, driveColor, markerT);
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

        _driveMeasure?.Dispose();
        _walkMeasure?.Dispose();

        _driveMeasure = new SKPathMeasure(_drive, forceClosed: false);
        _walkMeasure = new SKPathMeasure(_walk, forceClosed: false);

        _ringDash ??= SKPathEffect.CreateDash(RingDash, 0f);
        _walkDash ??= SKPathEffect.CreateDash(WalkDash, 0f);
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

    /// <summary>
    /// A pale casing under the route, drawn to the same progress as the line above it so the two
    /// grow together rather than the casing running ahead.
    /// </summary>
    private void DrawCasing(SKCanvas canvas, RouteRulerPalette palette, float driveT, float walkT)
    {
        _line.PathEffect = null;
        _line.StrokeWidth = CasingWidth;
        _line.Color = palette.NodeStroke.WithAlpha(210);

        DrawPartial(canvas, _drive, _driveMeasure, driveT);
        DrawPartial(canvas, _walk, _walkMeasure, walkT);
    }

    private void DrawRouteLines(
        SKCanvas canvas,
        RouteRulerPalette palette,
        SKColor driveColor,
        float driveT,
        float walkT)
    {
        _line.PathEffect = null;
        _line.StrokeWidth = RouteWidth;
        _line.Color = driveColor;
        DrawPartial(canvas, _drive, _driveMeasure, driveT);

        // Hollow, and amber: the mode change has to read without a legend.
        _line.PathEffect = _walkDash;
        _line.StrokeWidth = RouteWidth - 1f;
        _line.Color = palette.Amber;
        DrawPartial(canvas, _walk, _walkMeasure, walkT);
        _line.PathEffect = null;
    }

    /// <summary>
    /// Draws the first <paramref name="progress"/> of a path. Whole when settled, so the common
    /// case does not pay for a path extraction.
    /// </summary>
    private void DrawPartial(SKCanvas canvas, SKPath path, SKPathMeasure? measure, float progress)
    {
        if (progress <= 0f || path.IsEmpty)
        {
            return;
        }

        if (progress >= 1f || measure is null || measure.Length <= 0f)
        {
            canvas.DrawPath(path, _line);
            return;
        }

        using var segment = new SKPath();

        if (measure.GetSegment(0f, measure.Length * progress, segment, startWithMoveTo: true))
        {
            canvas.DrawPath(segment, _line);
        }
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
        SKColor driveColor,
        float progress)
    {
        var ticks = projection.Ticks;

        // The ticks arrive in order along the route rather than all at once, which reads as the
        // ruler being laid down over the line that has just drawn itself.
        var shown = (int)Math.Ceiling(ticks.Count * Ease(progress));

        for (var i = 0; i < shown && i < ticks.Count; i++)
        {
            var tick = ticks[i];
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

            Label(canvas, tick.Label, lx, ly, TextAnchor.Middle, ink, palette.Map, _labelFont);
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
        SKColor color,
        float progress)
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
        _stroke.Color = palette.Ink3.WithAlpha((byte)(70 * progress));
        canvas.DrawCircle(value.Center, RingRadius, _stroke);

        // -90 starts the sweep at twelve o'clock; Skia measures from three.
        _stroke.PathEffect = _ringDash;
        _stroke.StrokeWidth = RingWidth;
        _stroke.StrokeCap = SKStrokeCap.Round;
        _stroke.Color = color.WithAlpha((byte)(255 * progress));

        using var arc = new SKPath();
        arc.AddArc(box, -90f, value.SweepDegrees * progress);
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
            palette.Map,
            _labelFont);
    }

    // ---- pins and cursor ----------------------------------------------------------------------

    private void DrawMarkers(
        SKCanvas canvas,
        RulerProjection projection,
        RouteRulerPalette palette,
        SKColor driveColor,
        float pop)
    {
        DrawPin(canvas, projection.Origin, driveColor, palette.NodeStroke, pop);

        if (projection.HasWalk)
        {
            DrawPin(canvas, projection.Parking, palette.Amber, palette.NodeStroke, pop);
        }

        DrawPin(canvas, projection.Destination, palette.Pin, palette.NodeStroke, pop);
    }

    private void DrawPin(SKCanvas canvas, SKPoint at, SKColor fill, SKColor ring, float pop)
    {
        var radius = PinRadius * Math.Clamp(pop, 0f, 1.2f);

        if (radius <= 0f)
        {
            return;
        }

        _stroke.PathEffect = null;
        _stroke.Color = ring;
        _stroke.StrokeWidth = PinRingWidth;
        _stroke.StrokeCap = SKStrokeCap.Butt;
        canvas.DrawCircle(at, radius, _stroke);

        _fill.Color = fill;
        canvas.DrawCircle(at, radius, _fill);
    }

    /// <summary>
    /// Where the morning has got to. On a ring it rides the arc; on the road it sits on the route
    /// facing the way the car is going.
    /// </summary>
    private void DrawCursor(
        SKCanvas canvas,
        RulerProjection projection,
        RouteRulerPalette palette,
        SKColor driveColor,
        float progress)
    {
        var cursor = projection.Cursor;

        if (cursor.Kind == NowCursorKind.None || progress <= 0f)
        {
            return;
        }

        var at = cursor.Point;

        if (cursor.Kind is NowCursorKind.OnOriginRing or NowCursorKind.OnDestinationRing)
        {
            var angle = (cursor.RingAngleDegrees - 90f) * Math.PI / 180d;

            at = new SKPoint(
                cursor.Point.X + (float)(Math.Cos(angle) * RingRadius),
                cursor.Point.Y + (float)(Math.Sin(angle) * RingRadius));
        }

        var color = cursor.Kind == NowCursorKind.OnDestinationRing ? palette.Amber : driveColor;
        var radius = CursorRadius * Math.Clamp(progress, 0f, 1f);

        _fill.Color = color.WithAlpha(52);
        canvas.DrawCircle(at, radius * 2.4f, _fill);

        _stroke.PathEffect = null;
        _stroke.Color = palette.NodeStroke;
        _stroke.StrokeWidth = 2f;
        _stroke.StrokeCap = SKStrokeCap.Butt;
        canvas.DrawCircle(at, radius, _stroke);

        _fill.Color = color;
        canvas.DrawCircle(at, radius, _fill);
    }

    // ---- callouts -----------------------------------------------------------------------------

    /// <summary>
    /// The three times, anchored to the points they describe. These are the only text on the map
    /// besides the tick labels, so they carry a filled plate rather than a halo.
    /// </summary>
    private void DrawCallouts(
        SKCanvas canvas,
        RouteRulerSnapshot snapshot,
        RulerProjection projection,
        RouteRulerPalette palette,
        float progress,
        SKSize area)
    {
        var alpha = (byte)(255 * Math.Clamp(progress, 0f, 1f));
        var driveColor = snapshot.IsLate ? palette.Danger : palette.Accent;

        // A pin wearing a dwell ring is four times its own radius across, so its label has to clear
        // the ring rather than the pin - otherwise the arc is drawn straight through the text.
        Callout(
            canvas, snapshot.LeaveLabel, projection.Origin, driveColor, palette.OnAccent, alpha,
            above: false, area, Reach(projection.OriginDwell));

        // The parking spot and the door are a few hundred metres apart, which at the resolution a
        // whole commute fits into is a few pixels: their two callouts would sit on top of each
        // other. The bell is the one that matters, so the arrival label stands down rather than
        // both being drawn illegibly.
        if (projection.HasWalk && Apart(projection.Parking, projection.Destination) >= CalloutClearance)
        {
            Callout(
                canvas, snapshot.ArriveLabel, projection.Parking, palette.Amber, palette.AmberInk,
                alpha, above: true, area, PinRadius);
        }

        Callout(
            canvas, snapshot.BellLabel, projection.Destination, palette.Pin, palette.NodeStroke,
            alpha, above: true, area, Reach(projection.DestinationDwell));
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
        byte alpha,
        bool above,
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

        var top = above ? anchor.Y - offset - height : anchor.Y + offset;

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

        _fill.Color = plate.WithAlpha(alpha);
        canvas.DrawRoundRect(box, CalloutCornerRadius, CalloutCornerRadius, _fill);

        _fill.Color = ink.WithAlpha(alpha);
        canvas.DrawText(
            text,
            box.MidX - (width / 2f),
            box.MidY + (CalloutTextSize / 3f),
            _calloutFont,
            _fill);
    }

    /// <summary>
    /// Text with a halo in the map colour, so a label stays legible where it crosses a road or the
    /// route itself.
    /// </summary>
    private void Label(
        SKCanvas canvas,
        string text,
        float x,
        float y,
        TextAnchor anchor,
        SKColor color,
        SKColor halo,
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

        _stroke.PathEffect = null;
        _stroke.Color = halo.WithAlpha(color.Alpha);
        _stroke.StrokeWidth = 3f;
        _stroke.StrokeJoin = SKStrokeJoin.Round;
        canvas.DrawText(text, left, y, font, _stroke);

        _fill.Color = color;
        canvas.DrawText(text, left, y, font, _fill);
    }

    // ---- helpers ------------------------------------------------------------------------------

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

    public void Dispose()
    {
        _drive.Dispose();
        _walk.Dispose();
        _driveMeasure?.Dispose();
        _walkMeasure?.Dispose();
        _ringDash?.Dispose();
        _walkDash?.Dispose();
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
