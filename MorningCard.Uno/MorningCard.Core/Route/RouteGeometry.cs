namespace MorningCard.Core.Route;

/// <summary>Which mode a leg is travelled in. The ruler draws the two differently.</summary>
public enum RouteMode
{
    Drive,
    Walk,
}

/// <summary>
/// One straight run between two shape points, with what it costs to cover it.
/// </summary>
/// <param name="LengthMeters">Ground distance. Zero-length edges are dropped on load.</param>
/// <param name="DurationSeconds">Free-flow time from the routing asset, before reconciliation.</param>
public readonly record struct RouteEdge(double LengthMeters, double DurationSeconds);

/// <summary>
/// A projected shape point, in Spherical Mercator metres. The renderer projects these to screen;
/// nothing here knows about Mapsui or about pixels.
/// </summary>
public readonly record struct RoutePoint(double X, double Y);

/// <summary>
/// One mode of travel: the drive from home to the parking spot, or the walk from there to the door.
/// </summary>
/// <remarks>
/// Points has exactly one more entry than Edges, and the cumulative arrays match Points.
/// </remarks>
public sealed class RouteLeg
{
    public RouteLeg(RouteMode mode, IReadOnlyList<RoutePoint> points, IReadOnlyList<RouteEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(edges);

        if (points.Count != edges.Count + 1)
        {
            throw new ArgumentException(
                $"A leg needs one more point than edge; got {points.Count} points and {edges.Count} edges.",
                nameof(points));
        }

        Mode = mode;
        Points = points;
        Edges = edges;

        var times = new double[edges.Count + 1];
        var lengths = new double[edges.Count + 1];

        for (var i = 0; i < edges.Count; i++)
        {
            times[i + 1] = times[i] + edges[i].DurationSeconds;
            lengths[i + 1] = lengths[i] + edges[i].LengthMeters;
        }

        CumulativeSeconds = times;
        CumulativeMeters = lengths;
    }

    public RouteMode Mode { get; }

    public IReadOnlyList<RoutePoint> Points { get; }

    public IReadOnlyList<RouteEdge> Edges { get; }

    /// <summary>Time from the leg's start to each point. One entry per point.</summary>
    public IReadOnlyList<double> CumulativeSeconds { get; }

    /// <summary>Distance from the leg's start to each point. One entry per point.</summary>
    public IReadOnlyList<double> CumulativeMeters { get; }

    public double TotalSeconds => CumulativeSeconds[^1];

    public double TotalMeters => CumulativeMeters[^1];

    /// <summary>
    /// True when the leg covers no ground. A curbside drop-off has no walk, and nothing is drawn
    /// for it.
    /// </summary>
    public bool IsEmpty => Edges.Count == 0 || TotalMeters <= 0d;
}

/// <summary>
/// The whole trip's shape: the drive, then the walk. Built once from the committed route asset and
/// never refetched - the sample has no routing service, and the geometry does not change with the
/// clock.
/// </summary>
public sealed class RouteGeometry
{
    public RouteGeometry(RouteLeg drive, RouteLeg walk)
    {
        ArgumentNullException.ThrowIfNull(drive);
        ArgumentNullException.ThrowIfNull(walk);

        Drive = drive;
        Walk = walk;
    }

    public RouteLeg Drive { get; }

    /// <summary>Empty for a curbside drop-off, where the car stops at the door.</summary>
    public RouteLeg Walk { get; }

    /// <summary>Where the drive starts: home.</summary>
    public RoutePoint Origin => Drive.Points.Count > 0 ? Drive.Points[0] : default;

    /// <summary>Where the car stops and the walk begins.</summary>
    public RoutePoint Parking => Drive.Points.Count > 0 ? Drive.Points[^1] : default;

    /// <summary>The door. Same as the parking spot when there is no walk.</summary>
    public RoutePoint Destination => Walk.Points.Count > 0 ? Walk.Points[^1] : Parking;
}
