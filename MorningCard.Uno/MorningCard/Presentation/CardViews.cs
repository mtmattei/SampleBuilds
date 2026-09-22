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
/// <param name="WeatherSummary">Conditions and the day's high, e.g. "Clear · High 9°".</param>
/// <param name="NowText">"Now 7:24" in the status strip, so the card says what time it is reading.</param>
/// <param name="StatusBadge">"On time", "Leave now" or "Running late" - the state, named.</param>
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
    string OutsideTemp,
    string WeatherSummary,
    string NowText,
    string StatusBadge);

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
    string FailureMessage,
    int TotalMinutes,
    string ArrivalText,
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
    int SlackVisibleMinutes,
    string NowLabel,
    string LeaveLabel,
    string SlackLabel,
    string DriveLabel,
    string ArriveLabel,
    string WalkLabel,
    string BellLabel,
    string RouteDescription);

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
internal record TripResult(TripPlan? Plan, string FailureMessage)
{
    public bool Failed => Plan is null;

    public static TripResult Ok(TripPlan plan) => new(plan, string.Empty);

    public static TripResult Failure(string message) => new(null, message);
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
/// <param name="CabinFrom">Bolded in the row, so it is its own Run: "-3°".</param>
/// <param name="CabinTo">The other bolded Run: "20°".</param>
/// <param name="FuelNoteAccent">The half of the note that takes the severity colour, " · Fuel soon".</param>
/// <param name="FuelChip">The same state as a standalone chip label, without the separator.</param>
public record CarView(
    string FuelValue,
    string FuelUnit,
    string FuelNote,
    string FuelNoteAccent,
    string FuelChip,
    StatLevel FuelLevel,
    double FuelPercent,
    string CabinLabel,
    string CabinLead,
    string CabinFrom,
    string CabinMid,
    string CabinTo,
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
    bool IsStale);

