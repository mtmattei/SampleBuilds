using MorningCard.Core.Route;
using Xunit;

namespace MorningCard.Tests;

public class RouteGeometryTests
{
    /// <summary>
    /// A straight leg of <paramref name="edges"/> equal edges, each one metre east and one second
    /// long unless told otherwise. Straight and uniform so a failure is arithmetic, never geometry.
    /// </summary>
    private static RouteLeg Straight(RouteMode mode, int edges, double seconds = 1d, double meters = 1d)
    {
        var points = new List<RoutePoint>();
        var runs = new List<RouteEdge>();

        for (var i = 0; i <= edges; i++)
        {
            points.Add(new RoutePoint(i * meters, 0d));
        }

        for (var i = 0; i < edges; i++)
        {
            runs.Add(new RouteEdge(meters, seconds));
        }

        return new RouteLeg(mode, points, runs);
    }

    [Fact]
    public void Cumulatives_start_at_zero_and_end_at_the_total()
    {
        var leg = Straight(RouteMode.Drive, edges: 4, seconds: 10d, meters: 25d);

        Assert.Equal(5, leg.CumulativeSeconds.Count);
        Assert.Equal(0d, leg.CumulativeSeconds[0]);
        Assert.Equal(40d, leg.TotalSeconds);
        Assert.Equal(100d, leg.TotalMeters);
    }

    [Fact]
    public void A_leg_needs_one_more_point_than_edge()
    {
        var points = new List<RoutePoint> { new(0d, 0d), new(1d, 0d) };
        var edges = new List<RouteEdge> { new(1d, 1d), new(1d, 1d) };

        Assert.Throws<ArgumentException>(() => new RouteLeg(RouteMode.Drive, points, edges));
    }

    [Fact]
    public void A_point_inside_an_edge_is_interpolated()
    {
        var leg = Straight(RouteMode.Drive, edges: 4, seconds: 10d, meters: 100d);

        // 25 seconds in: two whole edges, then a quarter of the third.
        var (point, _) = leg.PointAtSeconds(25d);

        Assert.Equal(250d, point.X, 6);
        Assert.Equal(0d, point.Y, 6);
    }

    [Fact]
    public void A_point_landing_exactly_on_a_vertex_takes_that_vertex()
    {
        var leg = Straight(RouteMode.Drive, edges: 4, seconds: 10d, meters: 100d);

        var (point, _) = leg.PointAtSeconds(20d);

        Assert.Equal(200d, point.X, 6);
    }

    [Fact]
    public void A_point_past_the_end_clamps_to_the_last_vertex()
    {
        var leg = Straight(RouteMode.Drive, edges: 3, seconds: 10d, meters: 100d);

        // Reconciliation can leave a requested time a hair past the total; it must not run off.
        var (point, _) = leg.PointAtSeconds(1_000d);

        Assert.Equal(300d, point.X, 6);
    }

    [Fact]
    public void Bearing_follows_the_edge_the_point_sits_on()
    {
        var points = new List<RoutePoint> { new(0d, 0d), new(10d, 0d), new(10d, 10d) };
        var edges = new List<RouteEdge> { new(10d, 10d), new(10d, 10d) };
        var leg = new RouteLeg(RouteMode.Drive, points, edges);

        var (_, east) = leg.PointAtSeconds(5d);
        var (_, north) = leg.PointAtSeconds(15d);

        Assert.Equal(0d, east, 6);
        Assert.Equal(Math.PI / 2d, north, 6);
    }

    [Fact]
    public void Scaling_durations_keeps_the_shape_and_changes_the_total()
    {
        var points = new List<RoutePoint> { new(0d, 0d), new(100d, 0d), new(200d, 0d) };

        // A slow first edge and a fast second: the profile that tick spacing exists to show.
        var edges = new List<RouteEdge> { new(100d, 30d), new(100d, 10d) };
        var leg = new RouteLeg(RouteMode.Drive, points, edges);

        var scaled = leg.ScaleDurations(2d);

        Assert.Equal(80d, scaled.TotalSeconds, 6);

        // The slow edge is still three times the fast one.
        Assert.Equal(3d, scaled.Edges[0].DurationSeconds / scaled.Edges[1].DurationSeconds, 6);

        // And it covers the same ground as before.
        Assert.Equal(200d, scaled.TotalMeters, 6);
    }

    [Fact]
    public void Reconcile_matches_the_minutes_the_card_is_showing()
    {
        var geometry = new RouteGeometry(
            Straight(RouteMode.Drive, edges: 10, seconds: 60d, meters: 500d),
            Straight(RouteMode.Walk, edges: 4, seconds: 30d, meters: 50d));

        var reconciled = geometry.Reconcile(driveMinutes: 14, walkMinutes: 3);

        Assert.Equal(14d * 60d, reconciled.Drive.TotalSeconds, 6);
        Assert.Equal(3d * 60d, reconciled.Walk.TotalSeconds, 6);
    }

    [Fact]
    public void An_empty_leg_survives_reconciliation_rather_than_dividing_by_zero()
    {
        var walk = new RouteLeg(RouteMode.Walk, [new RoutePoint(0d, 0d)], []);
        var geometry = new RouteGeometry(Straight(RouteMode.Drive, edges: 4), walk);

        var reconciled = geometry.Reconcile(driveMinutes: 14, walkMinutes: 0);

        Assert.True(reconciled.Walk.IsEmpty);
        Assert.Equal(0d, reconciled.Walk.TotalSeconds);
    }

    [Fact]
    public void The_pins_come_off_the_ends_of_the_legs()
    {
        var geometry = new RouteGeometry(
            Straight(RouteMode.Drive, edges: 4, meters: 100d),
            new RouteLeg(
                RouteMode.Walk,
                [new RoutePoint(400d, 0d), new RoutePoint(400d, 50d)],
                [new RouteEdge(50d, 60d)]));

        Assert.Equal(0d, geometry.Origin.X, 6);
        Assert.Equal(400d, geometry.Parking.X, 6);
        Assert.Equal(50d, geometry.Destination.Y, 6);
    }

    [Fact]
    public void With_no_walk_the_door_is_the_parking_spot()
    {
        var walk = new RouteLeg(RouteMode.Walk, [new RoutePoint(400d, 0d)], []);
        var geometry = new RouteGeometry(Straight(RouteMode.Drive, edges: 4, meters: 100d), walk);

        Assert.Equal(geometry.Parking, geometry.Destination);
    }

    [Fact]
    public void Mercator_puts_the_origin_where_it_belongs()
    {
        var point = RouteAsset.Project(0d, 0d);

        Assert.Equal(0d, point.X, 6);
        Assert.Equal(0d, point.Y, 6);
    }

    [Fact]
    public void Mercator_matches_a_known_montreal_point()
    {
        // The card's drop-off, to the metre. A regression pin rather than an independent check:
        // what it catches is the projection silently changing, and a west-of-Greenwich, north-of-
        // equator point whose signs would flip if the formula were ever rewritten backwards.
        var point = RouteAsset.Project(-73.5795d, 45.5250d);

        Assert.Equal(-8_190_832.47d, point.X, tolerance: 1d);
        Assert.Equal(5_704_554.15d, point.Y, tolerance: 1d);
    }

    [Fact]
    public void A_malformed_asset_returns_null_rather_than_throwing()
    {
        Assert.Null(RouteAsset.TryParse("{ not json"));
        Assert.Null(RouteAsset.TryParse(string.Empty));
        Assert.Null(RouteAsset.TryParse("{}"));
    }

    [Fact]
    public void An_asset_parses_into_legs()
    {
        const string Json = """
        {
          "drive": {
            "coordinates": [[-73.60, 45.53], [-73.59, 45.53], [-73.58, 45.525]],
            "durations": [300, 540],
            "distances": [780, 1120]
          },
          "walk": {
            "coordinates": [[-73.58, 45.525], [-73.5795, 45.5250]],
            "durations": [180],
            "distances": [220]
          }
        }
        """;

        var geometry = RouteAsset.TryParse(Json);

        Assert.NotNull(geometry);
        Assert.Equal(2, geometry.Drive.Edges.Count);
        Assert.Single(geometry.Walk.Edges);
        Assert.Equal(840d, geometry.Drive.TotalSeconds, 6);
        Assert.Equal(1_900d, geometry.Drive.TotalMeters, 6);
    }

    [Fact]
    public void Zero_length_edges_are_dropped_on_load()
    {
        // OSRM repeats a coordinate at some junctions. Those runs carry no ground, so a tick can
        // never usefully land on them.
        const string Json = """
        {
          "drive": {
            "coordinates": [[-73.60, 45.53], [-73.60, 45.53], [-73.58, 45.525]],
            "durations": [0, 540],
            "distances": [0, 1120]
          }
        }
        """;

        var geometry = RouteAsset.TryParse(Json);

        Assert.NotNull(geometry);
        Assert.Single(geometry.Drive.Edges);
        Assert.Equal(1_120d, geometry.Drive.TotalMeters, 6);
    }

    [Fact]
    public void An_asset_with_no_walk_is_a_curbside_drop_off()
    {
        const string Json = """
        {
          "drive": {
            "coordinates": [[-73.60, 45.53], [-73.58, 45.525]],
            "durations": [840],
            "distances": [1900]
          }
        }
        """;

        var geometry = RouteAsset.TryParse(Json);

        Assert.NotNull(geometry);
        Assert.True(geometry.Walk.IsEmpty);
        Assert.Equal(geometry.Parking, geometry.Destination);
    }
}
