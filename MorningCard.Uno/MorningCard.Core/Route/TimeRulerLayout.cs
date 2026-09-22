using MorningCard.Core.Models;

namespace MorningCard.Core.Route;

/// <summary>Where a dwell happens. The two rings mean different things and read differently.</summary>
public enum DwellKind
{
    /// <summary>Waiting at home, before the car leaves.</summary>
    Origin,

    /// <summary>Waiting at the door, between arriving and the bell.</summary>
    Destination,
}

/// <summary>What the "now" marker is currently doing.</summary>
public enum NowCursorKind
{
    /// <summary>Nothing to mark: the morning is over, or there is no route.</summary>
    None,

    /// <summary>Still at home. The marker sweeps the origin ring.</summary>
    OnOriginRing,

    /// <summary>On the move. The marker travels the ruler.</summary>
    AlongRoute,

    /// <summary>Arrived, waiting for the bell. The marker sweeps the destination ring.</summary>
    OnDestinationRing,
}

/// <summary>
/// One minute mark on the route. Placed by time, so the gap between consecutive ticks is how far
/// the car gets in that minute - which is the whole device: ticks bunch where the drive is slow and
/// spread where it is fast.
/// </summary>
/// <param name="MinutesFromDeparture">Whole minutes since the car left.</param>
/// <param name="Label">Set on major ticks only; empty on minor ones.</param>
public readonly record struct RulerTick(
    RoutePoint Point,
    double BearingRadians,
    RouteMode Mode,
    bool IsMajor,
    int MinutesFromDeparture,
    string Label);

/// <summary>
/// Time spent standing still, drawn as an arc around the pin where the waiting happens. Clock
/// semantics: a full circle is sixty minutes, and the arc starts at twelve o'clock.
/// </summary>
/// <param name="OverflowLabel">
/// Set only past the hour, where the ring is already full and the arc can say no more.
/// </param>
public readonly record struct DwellRing(
    DwellKind Kind,
    RoutePoint Center,
    int Minutes,
    double SweepDegrees,
    string OverflowLabel);

/// <summary>
/// Where "now" sits. On a ring it is an angle; on the route it is a point and a heading.
/// </summary>
public readonly record struct NowCursor(
    NowCursorKind Kind,
    RoutePoint Point,
    double BearingRadians,
    double RingAngleDegrees);

/// <summary>Everything the renderer needs to draw the ruler, in world coordinates.</summary>
public sealed record RulerLayout(
    RouteGeometry Geometry,
    IReadOnlyList<RulerTick> Ticks,
    DwellRing? OriginDwell,
    DwellRing? DestinationDwell,
    NowCursor Cursor,
    int IntervalMinutes);

/// <summary>
/// Lays the morning's times onto the route's geometry.
///
/// Pure, and deliberately free of Mapsui and of Skia: it answers in Spherical Mercator metres, and
/// the renderer is the only thing that knows about pixels. That split is what lets every case the
/// ruler can draw - no slack, an hour of it, no walk leg, a tick landing exactly on a vertex - be
/// reached from a test rather than from a screenshot.
/// </summary>
public static class TimeRulerLayout
{
    /// <summary>A major tick, with a label, every this many minutes.</summary>
    public const int MajorEveryMin = 5;

    /// <summary>Intervals the ruler will consider, smallest first.</summary>
    public static readonly int[] CandidateIntervals = [1, 2, 5, 10];

    /// <summary>Minimum on-screen gap between ticks, in pixels, before the interval steps up.</summary>
    public const double MinTickGapPixels = 12d;

    /// <summary>A full ring is an hour, so past that the arc cannot say any more.</summary>
    public const int RingMinutesPerTurn = 60;

    /// <summary>
    /// How close to a pin a tick has to fall before it is dropped. Reconciliation scales durations
    /// by a ratio, so the handover rarely lands on a whole second; this is a hair, not a tolerance.
    /// </summary>
    private const double BoundaryEpsilonSeconds = 0.001d;

    public static RulerLayout Compute(
        RouteGeometry geometry,
        TripTimeline timeline,
        int walkMin,
        TimeOnly bell,
        TimeOnly now,
        int intervalMinutes)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(timeline);

        var interval = Math.Max(1, intervalMinutes);

        // The asset's free-flow durations set the shape of the speed profile; the card's minutes
        // set its length. After this the ruler and the timeline strip agree by construction.
        var scaled = geometry.Reconcile(timeline.DriveMin, walkMin);

        var ticks = BuildTicks(scaled, interval);

        return new RulerLayout(
            Geometry: scaled,
            Ticks: ticks,
            OriginDwell: BuildOriginDwell(scaled, timeline),
            DestinationDwell: BuildDestinationDwell(scaled, timeline, walkMin, bell),
            Cursor: BuildCursor(scaled, timeline, walkMin, bell, now),
            IntervalMinutes: interval);
    }

    /// <summary>
    /// Places a tick every <paramref name="interval"/> minutes from departure, running across the
    /// drive and straight on into the walk without restarting the count.
    /// </summary>
    /// <remarks>
    /// The tick at zero is not drawn: the leave pin already marks it, and a tick under a pin reads
    /// as a smudge. The one at the very end is dropped for the same reason.
    /// </remarks>
    private static List<RulerTick> BuildTicks(RouteGeometry geometry, int interval)
    {
        var ticks = new List<RulerTick>();

        var driveSeconds = geometry.Drive.TotalSeconds;
        var walkSeconds = geometry.Walk.TotalSeconds;
        var totalSeconds = driveSeconds + walkSeconds;

        if (totalSeconds <= 0d)
        {
            return ticks;
        }

        for (var minute = interval; ; minute += interval)
        {
            var seconds = minute * 60d;

            // Strictly inside: a tick exactly on the final vertex would sit under the arrival pin.
            if (seconds >= totalSeconds)
            {
                break;
            }

            // The handover is where the car parks, and that already has a pin on it. A tick landing
            // exactly there reads as a smudge under the pin, same as the ones at zero and at the
            // end, so it goes the same way.
            if (Math.Abs(seconds - driveSeconds) < BoundaryEpsilonSeconds)
            {
                continue;
            }

            // A leg with no ground gets no ticks - a curbside drop-off has no walk to mark, and
            // interpolating along it would stack every remaining tick on one point.
            var onWalk = seconds > driveSeconds;
            var leg = onWalk ? geometry.Walk : geometry.Drive;

            if (leg.IsEmpty)
            {
                continue;
            }

            var local = onWalk ? seconds - driveSeconds : seconds;
            var (point, bearing) = leg.PointAtSeconds(local);
            var isMajor = minute % MajorEveryMin == 0;

            ticks.Add(new RulerTick(
                Point: point,
                BearingRadians: bearing,
                Mode: leg.Mode,
                IsMajor: isMajor,
                MinutesFromDeparture: minute,
                Label: isMajor ? $"+{minute}" : string.Empty));
        }

        return ticks;
    }

    /// <summary>
    /// The wait at home, from now until the car has to leave.
    /// </summary>
    /// <remarks>
    /// Late means the car leaves this second, so there is no wait and no ring - the same rule the
    /// schematic ruler used for its dotted stretch.
    /// </remarks>
    private static DwellRing? BuildOriginDwell(RouteGeometry geometry, TripTimeline timeline) =>
        timeline.IsLate || timeline.SlackMin <= 0
            ? null
            : Ring(DwellKind.Origin, geometry.Origin, timeline.SlackMin);

    /// <summary>The wait at the door, between getting there and the bell.</summary>
    private static DwellRing? BuildDestinationDwell(
        RouteGeometry geometry,
        TripTimeline timeline,
        int walkMin,
        TimeOnly bell)
    {
        var minutes = MinutesBetween(AtDoor(timeline, walkMin), bell);

        return minutes <= 0 ? null : Ring(DwellKind.Destination, geometry.Destination, minutes);
    }

    private static DwellRing Ring(DwellKind kind, RoutePoint center, int minutes)
    {
        var turns = minutes / (double)RingMinutesPerTurn;

        // Past an hour the ring is full and a badge carries the rest; the arc has run out of
        // vocabulary, and a second lap would read as a shorter wait than the first.
        var sweep = Math.Min(turns, 1d) * 360d;
        var hours = minutes / RingMinutesPerTurn;

        return new DwellRing(
            Kind: kind,
            Center: center,
            Minutes: minutes,
            SweepDegrees: sweep,
            OverflowLabel: hours >= 1 ? $"+{hours}h" : string.Empty);
    }

    /// <summary>
    /// Where the morning has got to. Before departure the marker sweeps the origin ring, in motion
    /// it travels the ruler, and after arrival it sweeps the destination ring towards the bell.
    /// </summary>
    private static NowCursor BuildCursor(
        RouteGeometry geometry,
        TripTimeline timeline,
        int walkMin,
        TimeOnly bell,
        TimeOnly now)
    {
        var sinceDeparture = MinutesBetween(timeline.Start, now);

        if (sinceDeparture < 0)
        {
            // Still at home, and the two rings are not symmetrical.
            //
            // The destination ring spans two fixed times - door to bell - so a marker genuinely
            // sweeps across it. The origin ring spans now to leave, and its start IS the clock, so
            // the arc retracts as the morning goes on and a marker at its start would never move.
            // The marker therefore sits on the departure end, which closes on twelve o'clock and
            // reaches it exactly as the car pulls away.
            var slack = timeline.SlackMin;
            var swept = Math.Min(slack, RingMinutesPerTurn) / (double)RingMinutesPerTurn * 360d;

            return new NowCursor(
                NowCursorKind.OnOriginRing,
                geometry.Origin,
                BearingRadians: 0d,
                RingAngleDegrees: swept);
        }

        var totalSeconds = geometry.Drive.TotalSeconds + geometry.Walk.TotalSeconds;
        var seconds = sinceDeparture * 60d;

        if (seconds < totalSeconds)
        {
            var onWalk = seconds >= geometry.Drive.TotalSeconds;
            var leg = onWalk ? geometry.Walk : geometry.Drive;
            var local = onWalk ? seconds - geometry.Drive.TotalSeconds : seconds;
            var (point, bearing) = leg.PointAtSeconds(local);

            return new NowCursor(NowCursorKind.AlongRoute, point, bearing, RingAngleDegrees: 0d);
        }

        var waiting = MinutesBetween(AtDoor(timeline, walkMin), bell);

        if (waiting <= 0)
        {
            return new NowCursor(NowCursorKind.None, geometry.Destination, 0d, 0d);
        }

        var atDoorFor = MinutesBetween(AtDoor(timeline, walkMin), now);
        var progress = Math.Clamp(atDoorFor / (double)waiting, 0d, 1d);
        var full = Math.Min(waiting, RingMinutesPerTurn) / (double)RingMinutesPerTurn * 360d;

        return new NowCursor(
            NowCursorKind.OnDestinationRing,
            geometry.Destination,
            BearingRadians: 0d,
            RingAngleDegrees: progress * full);
    }

    /// <summary>When the child is actually at the door: the car's arrival plus the walk in.</summary>
    private static TimeOnly AtDoor(TripTimeline timeline, int walkMin) =>
        timeline.Arrive.AddMinutes(walkMin);

    /// <summary>
    /// Whole minutes from <paramref name="from"/> to <paramref name="to"/>, negative when
    /// <paramref name="to"/> is earlier.
    /// </summary>
    /// <remarks>
    /// <see cref="TimeOnly"/> subtraction wraps at midnight, which would turn "four minutes early"
    /// into most of a day. The card's own arithmetic is minutes-since-midnight for the same reason,
    /// so this matches it rather than trusting the wrap.
    /// </remarks>
    private static int MinutesBetween(TimeOnly from, TimeOnly to)
    {
        var fromMin = (from.Hour * 60) + from.Minute;
        var toMin = (to.Hour * 60) + to.Minute;

        return toMin - fromMin;
    }

    /// <summary>
    /// The smallest interval whose closest pair of ticks still stands apart on screen.
    ///
    /// Called once, at fit time: the map is pan- and zoom-locked, so the resolution the route is
    /// fitted at is the only one it will ever be drawn at.
    /// </summary>
    /// <param name="metersPerPixel">The map's resolution at the fitted view.</param>
    public static int ChooseInterval(RouteGeometry geometry, double metersPerPixel)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (metersPerPixel <= 0d)
        {
            return CandidateIntervals[^1];
        }

        foreach (var interval in CandidateIntervals)
        {
            if (MinimumGapMeters(geometry, interval) / metersPerPixel >= MinTickGapPixels)
            {
                return interval;
            }
        }

        return CandidateIntervals[^1];
    }

    /// <summary>
    /// The shortest straight-line distance between two consecutive ticks at this interval. That
    /// pair is the slowest stretch of the route, and it is what decides whether the interval reads.
    /// </summary>
    private static double MinimumGapMeters(RouteGeometry geometry, int interval)
    {
        var ticks = BuildTicks(geometry, interval);

        if (ticks.Count < 2)
        {
            return double.MaxValue;
        }

        var min = double.MaxValue;

        for (var i = 1; i < ticks.Count; i++)
        {
            var dx = ticks[i].Point.X - ticks[i - 1].Point.X;
            var dy = ticks[i].Point.Y - ticks[i - 1].Point.Y;

            min = Math.Min(min, Math.Sqrt((dx * dx) + (dy * dy)));
        }

        return min;
    }
}
