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
/// Points has exactly one more entry than Edges, and the cumulative arrays match Points. Holding
/// the cumulatives rather than recomputing them is what lets a tick find its edge by binary search
/// instead of walking the polyline.
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
    /// True when the leg covers no ground. A curbside drop-off has no walk, and the layout has to
    /// place no ticks on it rather than divide by its zero length.
    /// </summary>
    public bool IsEmpty => Edges.Count == 0 || TotalMeters <= 0d;

    /// <summary>
    /// The point reached at <paramref name="seconds"/> into the leg, interpolated along whichever
    /// edge that falls in, with the local bearing there.
    /// </summary>
    /// <remarks>
    /// Clamped at both ends: a tick landing exactly on the final vertex must not run off the array,
    /// and reconciliation can leave a requested time a hair past the total.
    /// </remarks>
    public (RoutePoint Point, double BearingRadians) PointAtSeconds(double seconds)
    {
        if (Points.Count == 0)
        {
            return (default, 0d);
        }

        if (Edges.Count == 0)
        {
            return (Points[0], 0d);
        }

        var index = FindEdge(seconds);
        var start = Points[index];
        var end = Points[index + 1];
        var span = Edges[index].DurationSeconds;

        // A zero-duration edge has no interior to interpolate; its start point is the whole answer.
        var fraction = span > 0d
            ? Math.Clamp((seconds - CumulativeSeconds[index]) / span, 0d, 1d)
            : 0d;

        var point = new RoutePoint(
            start.X + ((end.X - start.X) * fraction),
            start.Y + ((end.Y - start.Y) * fraction));

        return (point, Math.Atan2(end.Y - start.Y, end.X - start.X));
    }

    /// <summary>
    /// The index of the edge containing <paramref name="seconds"/>, by binary search over the
    /// cumulative times.
    /// </summary>
    private int FindEdge(double seconds)
    {
        if (seconds <= 0d)
        {
            return 0;
        }

        if (seconds >= TotalSeconds)
        {
            return Edges.Count - 1;
        }

        var low = 0;
        var high = Edges.Count - 1;

        while (low < high)
        {
            var mid = (low + high + 1) / 2;

            if (CumulativeSeconds[mid] <= seconds)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    /// <summary>
    /// This leg with every edge duration multiplied by <paramref name="factor"/>, so the speed
    /// profile survives while the total matches what the card says.
    /// </summary>
    public RouteLeg ScaleDurations(double factor)
    {
        if (Edges.Count == 0)
        {
            return this;
        }

        var scaled = new RouteEdge[Edges.Count];

        for (var i = 0; i < Edges.Count; i++)
        {
            scaled[i] = Edges[i] with { DurationSeconds = Edges[i].DurationSeconds * factor };
        }

        return new RouteLeg(Mode, Points, scaled);
    }
}

/// <summary>
/// The whole trip's shape: the drive, then the walk. Built once from the committed route asset and
/// never refetched - the sample has no routing service, and the geometry does not change with the
/// clock. Only the times laid over it do.
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

    /// <summary>
    /// Both legs rescaled so their totals match the minutes the card is showing. The asset's
    /// free-flow durations set the shape of the speed profile; the routine sets its length.
    /// </summary>
    /// <remarks>
    /// A leg whose asset total is zero cannot be scaled into one that is not - there is no profile
    /// to stretch - so it is left alone and the layout places no ticks on it.
    /// </remarks>
    public RouteGeometry Reconcile(double driveMinutes, double walkMinutes) =>
        new(ScaleLeg(Drive, driveMinutes), ScaleLeg(Walk, walkMinutes));

    private static RouteLeg ScaleLeg(RouteLeg leg, double targetMinutes)
    {
        var target = targetMinutes * 60d;

        if (leg.TotalSeconds <= 0d || target <= 0d)
        {
            return leg;
        }

        return leg.ScaleDurations(target / leg.TotalSeconds);
    }
}
