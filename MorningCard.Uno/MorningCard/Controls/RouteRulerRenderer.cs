using SkiaSharp;

namespace MorningCard.Controls;

/// <summary>
/// Draws the v6 route overlay on the map: the drive and walk legs on a paper casing, a dotted slack
/// line from now back to the leave point, rings where the drive starts and the walk starts, the now
/// dot, the school pin, and two callouts ("Now 7:24", "3 min walk").
///
/// No minute ticks since v6 (decision H6): the slack ruler owns the time scale and the map owns
/// geography. Measurements are the phone artboard's, in device-independent pixels.
/// </summary>
internal sealed class RouteRulerRenderer : IDisposable
{
    // ---- measurements --------------------------------------------------------------------------

    private const float DriveWidth = 6f;
    private const float WalkWidth = 4.5f;
    private const float CasingWidth = 10f;

    private const float SlackDotWidth = 3.5f;

    /// <summary>A round cap on a near-zero dash is a dot: 0.1 on, 8 off.</summary>
    private static readonly float[] SlackDash = [0.1f, 8f];

    /// <summary>
    /// How far one minute of slack pushes the now dot back from the leave point, capped so a long
    /// wait still leaves the dot on the panel. The artboard draws 9 minutes as about 88 px.
    /// </summary>
    private const float SlackPixelsPerMinute = 9.8f;

    private const float SlackMaxLength = 110f;

    private const float RingRadius = 6f;
    private const float RingWidth = 3f;

    private const float NowHaloRadius = 9f;
    private const float NowRadius = 6.5f;

    private const float PinRadius = 15f;
    private const float PinRingWidth = 2.5f;
    private const float PinGlyphSize = 17f;

    private const float CalloutTextSize = 12.5f;
    private const float CalloutPadX = 8f;
    private const float CalloutPadY = 4f;
    private const float CalloutLineHeight = 16f;
    private const float CalloutRadius = 6f;
    private const float CalloutMargin = 6f;

    /// <summary>The school's graduation cap in the Lucide font (Themes/Icons.xaml, IconSchool).</summary>
    private const string SchoolGlyph = "\uE234";

    // ---- reused drawing state ------------------------------------------------------------------

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    private readonly SKPaint _line = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly SKFont _calloutFont = new() { Size = CalloutTextSize, Edging = SKFontEdging.SubpixelAntialias };
    private readonly SKFont _iconFont = new() { Size = PinGlyphSize, Edging = SKFontEdging.Antialias };

    private readonly SKPath _drive = new();
    private readonly SKPath _walk = new();

    // Scratch paths, rewound per use: these were a native allocation per frame of the entrance.
    private readonly SKPath _segment = new();
    private readonly SKPath _slackPath = new();

    private SKPathMeasure? _driveMeasure;
    private SKPathMeasure? _walkMeasure;
    private SKPathEffect? _slackDash;

    /// <summary>The projection the cached paths were built from, compared by reference.</summary>
    private RulerProjection? _builtFor;

    public void SetTypeface(SKTypeface typeface) => _calloutFont.Typeface = typeface;

    public void SetIconTypeface(SKTypeface typeface) => _iconFont.Typeface = typeface;

    public void Render(SKCanvas canvas, SKSize area, RouteRulerSnapshot snapshot)
    {
        // A zero-sized first layout would feed NaN geometry to Skia, which is a native crash rather
        // than an exception. Nothing to draw at this size anyway.
        if (area.Width <= 0f || area.Height <= 0f || !snapshot.HasRoute || snapshot.Projection is not { } projection)
        {
            return;
        }

        EnsurePaths(projection);

        var palette = snapshot.Palette;
        var t = snapshot.Progress;

        // Trip order: the drive draws itself along the road, the walk follows, then the markers
        // land and the callouts settle.
        var driveT = Ease(Phase(t, 0.00, 0.45));
        var walkT = Ease(Phase(t, 0.42, 0.60));
        var markerT = Phase(t, 0.55, 0.80);
        var labelT = Phase(t, 0.75, 1.00);

        // Casing first, both legs, so the drive never crosses over the walk's paper edge.
        _line.PathEffect = null;
        _line.StrokeWidth = CasingWidth;
        _line.Color = palette.Map;
        DrawPartial(canvas, _drive, _driveMeasure, driveT);
        DrawPartial(canvas, _walk, _walkMeasure, walkT);

        _line.StrokeWidth = DriveWidth;
        _line.Color = palette.Accent;
        DrawPartial(canvas, _drive, _driveMeasure, driveT);

        _line.StrokeWidth = WalkWidth;
        _line.Color = palette.Amber;
        DrawPartial(canvas, _walk, _walkMeasure, walkT);

        var now = NowPoint(projection, snapshot.SlackMin, area);

        if (markerT <= 0f)
        {
            return;
        }

        var pop = BackOut(markerT);

        if (snapshot.SlackMin > 0)
        {
            DrawSlackLine(canvas, now, projection.Origin, palette, markerT);
            DrawRing(canvas, projection.Origin, palette, pop);
        }

        if (projection.HasWalk)
        {
            DrawRing(canvas, projection.Parking, palette, pop);
        }

        DrawSchool(canvas, projection.Destination, palette, pop);
        DrawNow(canvas, now, palette, snapshot.Tone, pop);

        if (labelT > 0f)
        {
            var alpha = (byte)(255 * Math.Clamp(labelT, 0f, 1f));
            var walkBox = DrawWalkCallout(canvas, snapshot.WalkLabel, projection.Destination, palette, alpha, area);
            DrawNowCallout(canvas, snapshot.NowLabel, now, snapshot.SlackMin > 0, projection.Destination, walkBox, palette, alpha, area);
        }
    }

    // ---- the route ----------------------------------------------------------------------------

    /// <summary>Rebuilds the cached paths when the projection changes, not per frame.</summary>
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

        _slackDash ??= SKPathEffect.CreateDash(SlackDash, 0f);
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

    /// <summary>Draws the first <paramref name="progress"/> of a path; whole when settled.</summary>
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

        _segment.Rewind();

        if (measure.GetSegment(0f, measure.Length * progress, _segment, startWithMoveTo: true))
        {
            canvas.DrawPath(_segment, _line);
        }
    }

    // ---- now and slack -------------------------------------------------------------------------

    /// <summary>
    /// Where "now" sits. The car has not moved yet, so with slack left the dot waits away from the
    /// leave point, one step per minute of slack; with none it sits on the leave point itself.
    /// </summary>
    /// <remarks>
    /// The artboard draws it back along the drive's first heading. On real geography that can run
    /// off the panel or under the Open in Maps pill, so headings fan out from there (±30° steps)
    /// and the first whose end lands in the clear part of the panel wins.
    /// </remarks>
    private static SKPoint NowPoint(RulerProjection projection, int slackMin, SKSize area)
    {
        var origin = projection.Origin;

        if (slackMin <= 0 || projection.Drive.Count < 2)
        {
            return origin;
        }

        // The first point far enough along to give a stable heading.
        var ahead = projection.Drive.FirstOrDefault(p => Distance(p, origin) > 4f, projection.Drive[^1]);
        var back = MathF.Atan2(origin.Y - ahead.Y, origin.X - ahead.X);
        var reach = Math.Min(slackMin * SlackPixelsPerMinute, SlackMaxLength);

        SKPoint At(float angle) => new(origin.X + (MathF.Cos(angle) * reach), origin.Y + (MathF.Sin(angle) * reach));

        foreach (var degrees in (ReadOnlySpan<float>)[0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f])
        {
            var candidate = At(back + (degrees * MathF.PI / 180f));

            if (IsClear(candidate, area) && DistanceToRoute(candidate, projection) > 16f)
            {
                return candidate;
            }
        }

        return At(back);
    }

    /// <summary>
    /// How far a point is from the drawn route. The now dot sitting on the drive line would read as
    /// the car already being on its way.
    /// </summary>
    private static float DistanceToRoute(SKPoint point, RulerProjection projection)
    {
        var best = float.MaxValue;

        foreach (var leg in new[] { projection.Drive, projection.Walk })
        {
            for (var i = 1; i < leg.Count; i++)
            {
                best = Math.Min(best, DistanceToSegment(point, leg[i - 1], leg[i]));
            }
        }

        return best;
    }

    private static float DistanceToSegment(SKPoint p, SKPoint a, SKPoint b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var lengthSquared = (abx * abx) + (aby * aby);

        if (lengthSquared <= 0f)
        {
            return Distance(p, a);
        }

        var t = Math.Clamp((((p.X - a.X) * abx) + ((p.Y - a.Y) * aby)) / lengthSquared, 0f, 1f);
        return Distance(p, new SKPoint(a.X + (t * abx), a.Y + (t * aby)));
    }

    /// <summary>
    /// Inside the panel with room for the callout, right of the ETA text (the left 38% of the panel
    /// under the opaque scrim), and out of the pill's bottom-right corner.
    /// </summary>
    private static bool IsClear(SKPoint point, SKSize area) =>
        point.X > area.Width * 0.38f
        && point.X < area.Width - 24f
        && point.Y > 44f
        && point.Y < area.Height - 24f
        && !(point.X > area.Width - 170f && point.Y > area.Height - 70f);

    private void DrawSlackLine(SKCanvas canvas, SKPoint from, SKPoint to, RouteRulerPalette palette, float progress)
    {
        _line.PathEffect = _slackDash;
        _line.StrokeWidth = SlackDotWidth;
        _line.Color = palette.SlackDot.WithAlpha((byte)(255 * Math.Clamp(progress, 0f, 1f)));
        // A path, not DrawLine: on this Skia build a dash effect on DrawLine painted nothing.
        _slackPath.Rewind();
        _slackPath.MoveTo(from);
        _slackPath.LineTo(to);
        canvas.DrawPath(_slackPath, _line);
        _line.PathEffect = null;
    }

    private void DrawNow(SKCanvas canvas, SKPoint at, RouteRulerPalette palette, SKColor tone, float pop)
    {
        var scale = Math.Clamp(pop, 0f, 1.2f);

        _fill.Color = palette.NodeStroke;
        canvas.DrawCircle(at, NowHaloRadius * scale, _fill);

        _fill.Color = tone;
        canvas.DrawCircle(at, NowRadius * scale, _fill);
    }

    // ---- rings and pin -------------------------------------------------------------------------

    /// <summary>A hollow ring in the drive colour: where the drive starts, and where it ends.</summary>
    private void DrawRing(SKCanvas canvas, SKPoint at, RouteRulerPalette palette, float pop)
    {
        var radius = RingRadius * Math.Clamp(pop, 0f, 1.2f);

        if (radius <= 0f)
        {
            return;
        }

        _fill.Color = palette.Surface;
        canvas.DrawCircle(at, radius, _fill);

        _stroke.PathEffect = null;
        _stroke.StrokeWidth = RingWidth;
        _stroke.Color = palette.Accent;
        canvas.DrawCircle(at, radius, _stroke);
    }

    /// <summary>The school: a dark disc on a white ring, carrying the graduation cap.</summary>
    private void DrawSchool(SKCanvas canvas, SKPoint at, RouteRulerPalette palette, float pop)
    {
        var scale = Math.Clamp(pop, 0f, 1.2f);
        var radius = PinRadius * scale;

        if (radius <= 0f)
        {
            return;
        }

        _fill.Color = palette.Pin;
        canvas.DrawCircle(at, radius, _fill);

        _stroke.PathEffect = null;
        _stroke.StrokeWidth = PinRingWidth;
        _stroke.Color = palette.NodeStroke;
        canvas.DrawCircle(at, radius, _stroke);

        if (_iconFont.Typeface is null || scale < 0.6f)
        {
            return;
        }

        _iconFont.Size = PinGlyphSize * scale;
        var width = _iconFont.MeasureText(SchoolGlyph, out var bounds);
        _fill.Color = palette.NodeStroke;
        canvas.DrawText(SchoolGlyph, at.X - (width / 2f), at.Y - bounds.MidY, _iconFont, _fill);
    }

    // ---- callouts ------------------------------------------------------------------------------

    /// <summary>
    /// "Now 7:24": above the dot while there is slack, to its left once leaving is now (the
    /// artboard's two places). On a small phone map the route is compact enough for either to land
    /// on the school pin or the walk callout, so below and right are tried next, and the first slot
    /// that is clear and inside the panel wins.
    /// </summary>
    private void DrawNowCallout(
        SKCanvas canvas,
        string text,
        SKPoint at,
        bool hasSlack,
        SKPoint school,
        SKRect walkBox,
        RouteRulerPalette palette,
        byte alpha,
        SKSize area)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var (width, height) = CalloutSize(text);
        var above = (at.X - (width / 2f), at.Y - 14f - height);
        var left = (at.X - 16f - width, at.Y - (height / 2f));
        var below = (at.X - (width / 2f), at.Y + 14f);
        var right = (at.X + 16f, at.Y - (height / 2f));

        var pin = new SKRect(school.X - PinRadius - 4f, school.Y - PinRadius - 4f, school.X + PinRadius + 4f, school.Y + PinRadius + 4f);
        var candidates = hasSlack ? new[] { above, below, left, right } : new[] { left, right, above, below };
        var chosen = candidates[0];

        foreach (var (x, y) in candidates)
        {
            var box = SKRect.Create(x, y, width, height);
            var inside = box.Left >= CalloutMargin && box.Top >= CalloutMargin
                && box.Right <= area.Width - CalloutMargin && box.Bottom <= area.Height - CalloutMargin;

            if (inside && !box.IntersectsWith(pin) && !box.IntersectsWith(walkBox))
            {
                chosen = (x, y);
                break;
            }
        }

        Callout(canvas, text, chosen.Item1, chosen.Item2, width, height, palette.Surface, palette.CalloutInk, palette, alpha, area);
    }

    /// <summary>"3 min walk": up and to the right of the school pin, as the artboard places it.</summary>
    private SKRect DrawWalkCallout(SKCanvas canvas, string text, SKPoint school, RouteRulerPalette palette, byte alpha, SKSize area)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SKRect.Empty;
        }

        var (width, height) = CalloutSize(text);
        return Callout(canvas, text, school.X - 5f, school.Y - PinRadius - 12f - height, width, height, palette.WalkPlate, palette.AmberInk, palette, alpha, area);
    }

    private (float Width, float Height) CalloutSize(string text) =>
        (_calloutFont.MeasureText(text) + (CalloutPadX * 2f), CalloutLineHeight + (CalloutPadY * 2f));

    private SKRect Callout(
        SKCanvas canvas,
        string text,
        float left,
        float top,
        float width,
        float height,
        SKColor plate,
        SKColor ink,
        RouteRulerPalette palette,
        byte alpha,
        SKSize area)
    {
        left = Math.Clamp(left, CalloutMargin, Math.Max(CalloutMargin, area.Width - width - CalloutMargin));
        top = Math.Clamp(top, CalloutMargin, Math.Max(CalloutMargin, area.Height - height - CalloutMargin));

        var box = SKRect.Create(left, top, width, height);

        _fill.Color = plate.WithAlpha(alpha);
        canvas.DrawRoundRect(box, CalloutRadius, CalloutRadius, _fill);

        _stroke.PathEffect = null;
        _stroke.StrokeWidth = 1f;
        _stroke.Color = palette.CalloutRing.WithAlpha((byte)(palette.CalloutRing.Alpha * alpha / 255));
        canvas.DrawRoundRect(box, CalloutRadius, CalloutRadius, _stroke);

        _fill.Color = ink.WithAlpha(alpha);
        _calloutFont.MeasureText(text, out var bounds);
        canvas.DrawText(text, box.Left + CalloutPadX, box.MidY - bounds.MidY, _calloutFont, _fill);

        return box;
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static float Distance(SKPoint a, SKPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

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
        _segment.Dispose();
        _slackPath.Dispose();
        _driveMeasure?.Dispose();
        _walkMeasure?.Dispose();
        _slackDash?.Dispose();
        _fill.Dispose();
        _stroke.Dispose();
        _line.Dispose();
        _calloutFont.Dispose();
        _iconFont.Dispose();
    }
}
