using MorningCard.Core.Models;

namespace MorningCard.Presentation;

/// <summary>
/// The three records the card binds to. They are deliberately flat and display-ready: MVUX generates
/// a bindable proxy for every type a feed carries, recursively, so nested records and nullable
/// members are where that generation breaks. Everything here is a string, a number, a bool or an
/// enum, which also keeps the XAML free of converters.
/// </summary>
/// <param name="HeadlineLead">Text before the accented span, e.g. "Leave in ".</param>
/// <param name="HeadlineAccent">
/// The span the design colours, e.g. "9 min". Split into three parts rather than carrying markup,
/// because the headline is three <c>Run</c>s in one TextBlock and a converter cannot produce Runs.
/// </param>
/// <param name="LockText">Sits under the car, e.g. "XC60 · Locked".</param>
/// <param name="WeatherLine">Under the greeting: "-3° · Clear · High 9°".</param>
/// <param name="NowText">"Now 7:24", alone at the top right. The status itself is the headline's job.</param>
/// <param name="LockAction">The lock button's label: the action it takes, "Unlock" or "Lock".</param>
public record HeaderView(
    string Greeting,
    string Title,
    string HeadlineLead,
    string HeadlineAccent,
    string HeadlineTail,
    CardTone Tone,
    bool IsStale,
    string CarName,
    string LockText,
    // The state itself, not only the sentence about it: the bar's lock button showed a shut
    // padlock while its own label read "Unlocked".
    bool IsLocked,
    string WeatherLine,
    string NowText,
    string LockAction);

/// <summary>
/// The ETA block and the route ruler. Reached only when there is a trip: no trip means the feed is
/// none, which is what draws the empty state.
/// </summary>
/// <param name="RouteLeave">Where the drive starts along the path, 0 to 1.</param>
/// <param name="RouteArrive">Where the walk starts along the path, 0 to 1.</param>
/// <param name="LeaveByTime">Bare "7:33" for the timeline strip, which supplies its own labels.</param>
/// <param name="LeaveByLabel">"LEAVE BY" normally, "LEAVE" once the answer is "Now".</param>
public record EtaView(
    bool Failed,
    // The live ETA has been asked for and has not answered. A value rather than a FeedView
    // progress state: this feed combines a push stream, which never reports progress before its
    // first element, so a ProgressTemplate here is unreachable.
    bool IsLoading,
    // Neither failed nor loading: the route, the scrim under the ETA block and Open in Maps show.
    bool ShowsRoute,
    string FailureMessage,
    // The drive alone. The countdown lives in the headline and the slack on the ruler, so each
    // of the three durations appears once.
    int DriveMinutes,
    TrafficLevel Traffic,
    string TrafficText,
    string VsUsualText,
    bool IsLate,
    double RouteLeave,
    double RouteArrive,
    CardTone Tone,
    string LeaveByTime,
    string LeaveByLabel,
    string ArriveTime,
    string BellTime,
    // The strip's three track segments, as star weights. Computed here rather than in a converter
    // because the middle one is a span between two fractions and a value converter sees only one.
    double TrackSlack,
    double TrackDrive,
    double TrackWalk,
    string LeaveLabel,
    string RouteDescription,
    // Null on a failed lookup, which has no trip to lay on the map.
    RulerPlan? Ruler = null);

/// <summary>
/// What the route ruler needs from the morning, handed over whole rather than pre-computed.
///
/// The layout cannot be built here: the tick interval depends on the resolution the map is fitted
/// at, which only the view knows. So the model supplies the times and the view supplies the map.
/// </summary>
public record RulerPlan(TripTimeline Timeline, int WalkMin, TimeOnly Bell, TimeOnly Now);

/// <summary>
/// Wraps the trip so a no-trip morning stays a value on the header's feed. Without it the null
/// becomes a none state and the header loses the car status it still has to show.
/// </summary>
internal record TripSlot(TripPlan? Plan);

/// <summary>
/// One attempt at reading the trip: either a plan, or the reason there isn't one.
///
/// The failure travels as a value rather than as a feed error because a feed built on a push stream
/// cannot be recovered by FeedView.Refresh: measured on 2026-09-21, the Retry button executed with
/// CanExecute true and the card stayed on its error template, because refreshing does not
/// re-subscribe a stream that has already thrown. Carrying the failure keeps the stream alive, so
/// the model's own retry can put a good value through it.
/// </summary>
internal record TripResult(TripPlan? Plan, string FailureMessage, bool Pending = false)
{
    public bool Failed => Plan is null && !Pending;

    public static TripResult Ok(TripPlan plan) => new(plan, string.Empty);

    public static TripResult Failure(string message) => new(null, message);

    /// <summary>Asked for, not yet answered: what the Loading skeleton renders.</summary>
    public static TripResult Loading { get; } = new(null, string.Empty, Pending: true);
}

/// <summary>
/// One connection mode as the chips show it. A chip renders its item's ToString alongside any
/// ItemTemplate, so the two overlapped when the enum was bound directly; naming the option is the
/// simpler fix and keeps selection typed.
/// </summary>
public record ModeOption(ConnectionMode Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The three list rows under the map: Fuel, Cabin, and the destination.
///
/// Flat strings on purpose. MVUX generates a bindable proxy for every type a feed carries,
/// recursively, so a list of row records would pull the generator into exactly the shapes it fails
/// on. Three rows is a fixed number the design sets, not a collection.
/// </summary>
/// <param name="FuelNoteAccent">The half of the note that takes the severity colour, " · Fuel soon".</param>
/// <param name="FuelChip">The same state as a standalone chip label, without the separator.</param>
/// <param name="FuelTitle">"Enough for today", or the warning when it is not.</param>
public record CarView(
    string FuelValue,
    string FuelUnit,
    string FuelNote,
    string FuelNoteAccent,
    string FuelChip,
    StatLevel FuelLevel,
    string FuelTitle,
    string CabinLabel,
    string CabinLead,
    string CabinAccent,
    // The checklist header: how many checks there are and how many are ready.
    string ChecksCount,
    string ChecksReady,
    bool CabinNeedsWarming,
    bool ChecksExpanded,
    string ParkingChip,
    string DestinationLabel,
    string ParkingStreet,
    string ParkingNote,
    bool HasDestination,
    bool IsStale,
    CardTone Tone);

