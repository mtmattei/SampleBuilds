using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Route;
using Xunit;

namespace MorningCard.Tests;

public class TimeRulerLayoutTests
{
    private const int DefaultWalkMin = 3;

    private static readonly TimeOnly Bell = new(7, 50);

    private static TripPlan Plan(int delayMin = 0, int walkMin = DefaultWalkMin) =>
        new("School drop-off", Bell, 14, 16, delayMin, walkMin);

    /// <summary>
    /// A straight leg of equal edges. Uniform on purpose: anywhere a test asserts on spacing, an
    /// uneven profile would hide whether the arithmetic or the geometry moved.
    /// </summary>
    private static RouteLeg Straight(RouteMode mode, int edges, double meters = 100d)
    {
        var points = new List<RoutePoint>();
        var runs = new List<RouteEdge>();

        for (var i = 0; i <= edges; i++)
        {
            points.Add(new RoutePoint(i * meters, 0d));
        }

        for (var i = 0; i < edges; i++)
        {
            runs.Add(new RouteEdge(meters, 60d));
        }

        return new RouteLeg(mode, points, runs);
    }

    private static RouteGeometry Geometry(int driveEdges = 14, int walkEdges = 3) =>
        new(
            Straight(RouteMode.Drive, driveEdges),
            walkEdges == 0
                ? new RouteLeg(RouteMode.Walk, [new RoutePoint(driveEdges * 100d, 0d)], [])
                : Straight(RouteMode.Walk, walkEdges, meters: 20d));

    private static RulerLayout Layout(
        TimeOnly now,
        int delayMin = 0,
        int walkMin = DefaultWalkMin,
        int walkEdges = 3,
        int interval = 1)
    {
        var plan = Plan(delayMin, walkMin);
        var timeline = TripMath.Derive(plan, now);

        return TimeRulerLayout.Compute(Geometry(walkEdges: walkEdges), timeline, walkMin, Bell, now, interval);
    }

    // ---- ticks -------------------------------------------------------------------------------

    [Fact]
    public void Ticks_run_across_the_drive_and_on_into_the_walk()
    {
        var layout = Layout(new TimeOnly(7, 24));

        // 14 drive + 3 walk = 17 minutes. Ticks at 1..16, less the one at 14: that is the handover,
        // where the parking pin already sits. The one at 17 would sit under the arrival pin.
        Assert.Equal(15, layout.Ticks.Count);
        Assert.Equal(1, layout.Ticks[0].MinutesFromDeparture);
        Assert.Equal(16, layout.Ticks[^1].MinutesFromDeparture);
        Assert.DoesNotContain(layout.Ticks, t => t.MinutesFromDeparture == 14);
    }

    [Fact]
    public void The_count_does_not_restart_on_the_walk()
    {
        var layout = Layout(new TimeOnly(7, 24));

        var firstWalkTick = layout.Ticks.First(t => t.Mode == RouteMode.Walk);

        // The walk starts 14 minutes after departure, so its first tick is the fifteenth.
        Assert.Equal(15, firstWalkTick.MinutesFromDeparture);
    }

    [Fact]
    public void Every_fifth_tick_is_major_and_carries_its_label()
    {
        var layout = Layout(new TimeOnly(7, 24));

        var majors = layout.Ticks.Where(t => t.IsMajor).ToList();

        Assert.Equal([5, 10, 15], majors.Select(t => t.MinutesFromDeparture));
        Assert.Equal(["+5", "+10", "+15"], majors.Select(t => t.Label));
        Assert.All(layout.Ticks.Where(t => !t.IsMajor), t => Assert.Equal(string.Empty, t.Label));
    }

    [Fact]
    public void Tick_spacing_is_what_carries_speed()
    {
        // A slow first half and a fast second, over the same ground: the whole point of the device.
        var points = new List<RoutePoint>();
        var edges = new List<RouteEdge>();

        for (var i = 0; i <= 10; i++)
        {
            points.Add(new RoutePoint(i * 100d, 0d));
        }

        for (var i = 0; i < 10; i++)
        {
            edges.Add(new RouteEdge(100d, i < 5 ? 120d : 30d));
        }

        var geometry = new RouteGeometry(
            new RouteLeg(RouteMode.Drive, points, edges),
            new RouteLeg(RouteMode.Walk, [new RoutePoint(1_000d, 0d)], []));

        var timeline = TripMath.Derive(Plan(walkMin: 0), new TimeOnly(7, 24));
        var layout = TimeRulerLayout.Compute(geometry, timeline, 0, Bell, new TimeOnly(7, 24), 1);

        var ticks = layout.Ticks;
        var firstGap = ticks[1].Point.X - ticks[0].Point.X;
        var lastGap = ticks[^1].Point.X - ticks[^2].Point.X;

        // Same minute, four times the ground, because the second half runs four times faster.
        Assert.True(lastGap > firstGap * 3d, $"expected the fast stretch to spread: {firstGap} then {lastGap}");
    }

    [Fact]
    public void A_curbside_drop_off_puts_no_ticks_on_the_missing_walk()
    {
        var layout = Layout(new TimeOnly(7, 24), walkMin: 0, walkEdges: 0);

        Assert.All(layout.Ticks, t => Assert.Equal(RouteMode.Drive, t.Mode));
        Assert.Equal(13, layout.Ticks.Count);
    }

    [Fact]
    public void A_wider_interval_thins_the_ticks()
    {
        var layout = Layout(new TimeOnly(7, 24), interval: 5);

        Assert.Equal([5, 10, 15], layout.Ticks.Select(t => t.MinutesFromDeparture));
        Assert.All(layout.Ticks, t => Assert.True(t.IsMajor));
    }

    // ---- dwell rings -------------------------------------------------------------------------

    [Fact]
    public void Slack_becomes_an_arc_at_the_origin()
    {
        var layout = Layout(new TimeOnly(7, 24));

        var ring = Assert.IsType<DwellRing>(layout.OriginDwell);

        // 7:50 bell, less 3 walk and 14 drive, is a 7:33 departure: 9 minutes of slack.
        Assert.Equal(9, ring.Minutes);
        Assert.Equal(9d / 60d * 360d, ring.SweepDegrees, 6);
        Assert.Equal(layout.Geometry.Origin, ring.Center);
        Assert.Equal(string.Empty, ring.OverflowLabel);
    }

    [Fact]
    public void Running_late_leaves_no_origin_ring()
    {
        // 7:40 against a 7:33 departure: the car leaves this second, so there is no wait to draw.
        var layout = Layout(new TimeOnly(7, 40));

        Assert.Null(layout.OriginDwell);
    }

    [Fact]
    public void Arriving_exactly_at_the_bell_leaves_no_destination_ring()
    {
        var layout = Layout(new TimeOnly(7, 24));

        // Leave-by arithmetic puts the door at the bell precisely, so there is nothing to wait out.
        Assert.Null(layout.DestinationDwell);
    }

    [Fact]
    public void An_early_start_waits_at_the_door()
    {
        var plan = Plan();
        var timeline = TripMath.Derive(plan, new TimeOnly(7, 24));

        // Same geometry, but a bell ten minutes later than the trip needs.
        var later = new TimeOnly(8, 0);
        var layout = TimeRulerLayout.Compute(
            Geometry(), timeline, DefaultWalkMin, later, new TimeOnly(7, 24), 1);

        var ring = Assert.IsType<DwellRing>(layout.DestinationDwell);

        Assert.Equal(10, ring.Minutes);
        Assert.Equal(DwellKind.Destination, ring.Kind);
        Assert.Equal(layout.Geometry.Destination, ring.Center);
    }

    [Fact]
    public void An_hour_fills_the_ring_exactly()
    {
        var plan = Plan();
        var timeline = TripMath.Derive(plan, new TimeOnly(7, 24));

        var layout = TimeRulerLayout.Compute(
            Geometry(), timeline, DefaultWalkMin, new TimeOnly(8, 50), new TimeOnly(7, 24), 1);

        var ring = Assert.IsType<DwellRing>(layout.DestinationDwell);

        Assert.Equal(60, ring.Minutes);
        Assert.Equal(360d, ring.SweepDegrees, 6);
        Assert.Equal("+1h", ring.OverflowLabel);
    }

    [Fact]
    public void Past_an_hour_the_ring_stays_full_and_a_badge_carries_the_rest()
    {
        var plan = Plan();
        var timeline = TripMath.Derive(plan, new TimeOnly(7, 24));

        // 2h20 of waiting: a second lap would read as a shorter wait than the first.
        var layout = TimeRulerLayout.Compute(
            Geometry(), timeline, DefaultWalkMin, new TimeOnly(10, 10), new TimeOnly(7, 24), 1);

        var ring = Assert.IsType<DwellRing>(layout.DestinationDwell);

        Assert.Equal(140, ring.Minutes);
        Assert.Equal(360d, ring.SweepDegrees, 6);
        Assert.Equal("+2h", ring.OverflowLabel);
    }

    // ---- now cursor --------------------------------------------------------------------------

    [Fact]
    public void Before_departure_the_cursor_marks_the_departure_end_of_the_ring()
    {
        var layout = Layout(new TimeOnly(7, 24));

        var ring = Assert.IsType<DwellRing>(layout.OriginDwell);

        Assert.Equal(NowCursorKind.OnOriginRing, layout.Cursor.Kind);
        Assert.Equal(layout.Geometry.Origin, layout.Cursor.Point);
        Assert.Equal(ring.SweepDegrees, layout.Cursor.RingAngleDegrees, 6);
    }

    [Fact]
    public void The_origin_ring_retracts_towards_twelve_as_the_wait_runs_out()
    {
        // The origin ring spans now to leave, so its start is the clock: it shortens rather than
        // filling, and the marker on its far end closes on twelve o'clock.
        var early = Layout(new TimeOnly(7, 24));
        var late = Layout(new TimeOnly(7, 30));

        Assert.Equal(9, Assert.IsType<DwellRing>(early.OriginDwell).Minutes);
        Assert.Equal(3, Assert.IsType<DwellRing>(late.OriginDwell).Minutes);
        Assert.True(late.Cursor.RingAngleDegrees < early.Cursor.RingAngleDegrees);
    }

    [Fact]
    public void In_motion_the_cursor_travels_the_ruler()
    {
        // Late, so the drive starts now; five minutes on it is five minutes along the route.
        var plan = Plan();
        var timeline = TripMath.Derive(plan, new TimeOnly(7, 40));
        var geometry = Geometry();

        var layout = TimeRulerLayout.Compute(
            geometry, timeline, DefaultWalkMin, Bell, timeline.Start.AddMinutes(5), 1);

        Assert.Equal(NowCursorKind.AlongRoute, layout.Cursor.Kind);

        // Five of fourteen drive minutes, over a uniform 1400m leg.
        Assert.Equal(500d, layout.Cursor.Point.X, 4);
    }

    [Fact]
    public void After_arrival_the_cursor_moves_to_the_destination_ring()
    {
        var plan = Plan();
        var timeline = TripMath.Derive(plan, new TimeOnly(7, 24));

        // Door at 7:50, bell at 8:00, and it is 7:55: halfway through the wait.
        var layout = TimeRulerLayout.Compute(
            Geometry(), timeline, DefaultWalkMin, new TimeOnly(8, 0), new TimeOnly(7, 55), 1);

        var ring = Assert.IsType<DwellRing>(layout.DestinationDwell);

        Assert.Equal(NowCursorKind.OnDestinationRing, layout.Cursor.Kind);
        Assert.Equal(ring.SweepDegrees / 2d, layout.Cursor.RingAngleDegrees, 4);
    }

    // ---- reconciliation ----------------------------------------------------------------------

    [Fact]
    public void Traffic_stretches_the_route_without_reshaping_it()
    {
        var clear = Layout(new TimeOnly(7, 24));
        var heavy = Layout(new TimeOnly(7, 24), delayMin: 6);

        Assert.Equal(14d * 60d, clear.Geometry.Drive.TotalSeconds, 6);
        Assert.Equal(20d * 60d, heavy.Geometry.Drive.TotalSeconds, 6);

        // Same ground either way: delay is time, not distance.
        Assert.Equal(clear.Geometry.Drive.TotalMeters, heavy.Geometry.Drive.TotalMeters, 6);

        // And more ticks, because the drive now takes longer.
        Assert.True(heavy.Ticks.Count > clear.Ticks.Count);
    }

    [Fact]
    public void The_ruler_and_the_strip_agree_on_the_totals()
    {
        var plan = Plan(delayMin: 4);
        var timeline = TripMath.Derive(plan, new TimeOnly(7, 24));
        var layout = TimeRulerLayout.Compute(
            Geometry(), timeline, plan.WalkMin, Bell, new TimeOnly(7, 24), 1);

        var rulerMinutes =
            (layout.Geometry.Drive.TotalSeconds + layout.Geometry.Walk.TotalSeconds) / 60d;

        Assert.Equal(timeline.TotalMin, rulerMinutes, 6);
    }

    // ---- interval selection ------------------------------------------------------------------

    [Fact]
    public void A_roomy_view_keeps_the_finest_interval()
    {
        // Uniform 100m per minute at 1m/px is 100px between ticks: far past the 12px floor.
        Assert.Equal(1, TimeRulerLayout.ChooseInterval(Geometry(), metersPerPixel: 1d));
    }

    [Fact]
    public void A_tight_view_steps_the_interval_up_until_the_ticks_stand_apart()
    {
        // 100m per minute at 20m/px is 5px: too close at one minute, and at two, so five it is.
        Assert.Equal(5, TimeRulerLayout.ChooseInterval(Geometry(), metersPerPixel: 20d));
    }

    [Fact]
    public void The_interval_never_runs_past_the_coarsest_candidate()
    {
        Assert.Equal(10, TimeRulerLayout.ChooseInterval(Geometry(), metersPerPixel: 10_000d));
        Assert.Equal(10, TimeRulerLayout.ChooseInterval(Geometry(), metersPerPixel: 0d));
    }
}
