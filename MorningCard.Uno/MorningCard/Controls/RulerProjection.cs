using Mapsui;
using Mapsui.Extensions;
using MorningCard.Core.Route;
using SkiaSharp;

namespace MorningCard.Controls;

/// <summary>A tick as the renderer needs it: a point, a heading, and how loud to draw it.</summary>
public readonly record struct ProjectedTick(
    SKPoint Point,
    float BearingRadians,
    RouteMode Mode,
    bool IsMajor,
    string Label);

/// <summary>A dwell arc, centred on a pin, in screen space.</summary>
public readonly record struct ProjectedRing(
    DwellKind Kind,
    SKPoint Center,
    float SweepDegrees,
    string OverflowLabel);

/// <summary>Where "now" is, once projected.</summary>
public readonly record struct ProjectedCursor(
    NowCursorKind Kind,
    SKPoint Point,
    float BearingRadians,
    float RingAngleDegrees);

/// <summary>
/// The ruler in screen space: the route as pixels, ready to draw.
///
/// The projection happens on the UI thread, where the map's viewport is safe to read. The render
/// thread only ever sees this - plain points and floats, no Mapsui, no DependencyProperty, nothing
/// that can move underneath it mid-frame.
/// </summary>
public sealed record RulerProjection
{
    public IReadOnlyList<SKPoint> Drive { get; init; } = [];

    public IReadOnlyList<SKPoint> Walk { get; init; } = [];

    public IReadOnlyList<ProjectedTick> Ticks { get; init; } = [];

    public ProjectedRing? OriginDwell { get; init; }

    public ProjectedRing? DestinationDwell { get; init; }

    public ProjectedCursor Cursor { get; init; }

    /// <summary>Home, where the drive starts.</summary>
    public SKPoint Origin { get; init; }

    /// <summary>Where the car stops.</summary>
    public SKPoint Parking { get; init; }

    /// <summary>The door.</summary>
    public SKPoint Destination { get; init; }

    /// <summary>False when the walk covers no ground, as at a curbside drop-off.</summary>
    public bool HasWalk { get; init; }

    /// <summary>
    /// Projects a layout through the map's viewport.
    /// </summary>
    /// <remarks>
    /// Headings are taken by projecting a second point a metre further along the route rather than
    /// by flipping the world bearing's sign. Screen Y runs the opposite way to Mercator Y, and the
    /// viewport can be rotated; going through the same transform twice gets both right without this
    /// code having to know about either.
    /// </remarks>
    public static RulerProjection Create(RulerLayout layout, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var geometry = layout.Geometry;

        return new RulerProjection
        {
            Drive = ProjectPoints(geometry.Drive.Points, viewport),
            Walk = geometry.Walk.IsEmpty ? [] : ProjectPoints(geometry.Walk.Points, viewport),
            Ticks = ProjectTicks(layout.Ticks, viewport),
            OriginDwell = ProjectRing(layout.OriginDwell, viewport),
            DestinationDwell = ProjectRing(layout.DestinationDwell, viewport),
            Cursor = ProjectCursor(layout.Cursor, viewport),
            Origin = ToScreen(geometry.Origin, viewport),
            Parking = ToScreen(geometry.Parking, viewport),
            Destination = ToScreen(geometry.Destination, viewport),
            HasWalk = !geometry.Walk.IsEmpty,
        };
    }

    private static SKPoint[] ProjectPoints(IReadOnlyList<RoutePoint> points, Viewport viewport)
    {
        var screen = new SKPoint[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            screen[i] = ToScreen(points[i], viewport);
        }

        return screen;
    }

    private static ProjectedTick[] ProjectTicks(IReadOnlyList<RulerTick> ticks, Viewport viewport)
    {
        var projected = new ProjectedTick[ticks.Count];

        for (var i = 0; i < ticks.Count; i++)
        {
            var tick = ticks[i];

            projected[i] = new ProjectedTick(
                Point: ToScreen(tick.Point, viewport),
                BearingRadians: ScreenBearing(tick.Point, tick.BearingRadians, viewport),
                Mode: tick.Mode,
                IsMajor: tick.IsMajor,
                Label: tick.Label);
        }

        return projected;
    }

    private static ProjectedRing? ProjectRing(DwellRing? ring, Viewport viewport) =>
        ring is { } value
            ? new ProjectedRing(
                value.Kind,
                ToScreen(value.Center, viewport),
                (float)value.SweepDegrees,
                value.OverflowLabel)
            : null;

    private static ProjectedCursor ProjectCursor(NowCursor cursor, Viewport viewport) =>
        new(
            cursor.Kind,
            ToScreen(cursor.Point, viewport),
            ScreenBearing(cursor.Point, cursor.BearingRadians, viewport),
            (float)cursor.RingAngleDegrees);

    private static SKPoint ToScreen(RoutePoint point, Viewport viewport)
    {
        var screen = viewport.WorldToScreen(point.X, point.Y);

        return new SKPoint((float)screen.X, (float)screen.Y);
    }

    /// <summary>
    /// The heading at a point, measured in screen space by stepping a metre along the world bearing
    /// and projecting both ends.
    /// </summary>
    private static float ScreenBearing(RoutePoint point, double worldBearing, Viewport viewport)
    {
        const double StepMeters = 1d;

        var ahead = new RoutePoint(
            point.X + (Math.Cos(worldBearing) * StepMeters),
            point.Y + (Math.Sin(worldBearing) * StepMeters));

        var from = ToScreen(point, viewport);
        var to = ToScreen(ahead, viewport);

        return (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
    }
}
