using Mapsui;
using Mapsui.Extensions;
using MorningCard.Core.Route;
using SkiaSharp;

namespace MorningCard.Controls;

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

    /// <summary>Home, where the drive starts.</summary>
    public SKPoint Origin { get; init; }

    /// <summary>Where the car stops.</summary>
    public SKPoint Parking { get; init; }

    /// <summary>The door.</summary>
    public SKPoint Destination { get; init; }

    /// <summary>False when the walk covers no ground, as at a curbside drop-off.</summary>
    public bool HasWalk { get; init; }

    /// <summary>
    /// Projects the route through the map's viewport. The v6 overlay draws the legs and three
    /// points only: no ticks, dwell rings or cursor (H6), so the geometry goes straight through.
    /// </summary>
    public static RulerProjection Create(RouteGeometry geometry, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return new RulerProjection
        {
            Drive = ProjectPoints(geometry.Drive.Points, viewport),
            Walk = geometry.Walk.IsEmpty ? [] : ProjectPoints(geometry.Walk.Points, viewport),
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

    private static SKPoint ToScreen(RoutePoint point, Viewport viewport)
    {
        var screen = viewport.WorldToScreen(point.X, point.Y);

        return new SKPoint((float)screen.X, (float)screen.Y);
    }
}
