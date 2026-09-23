using MorningCard.Core;
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
/// <param name="WeatherLine">"-3° · Clear · High 9°", under the greeting.</param>
/// <param name="NowText">"Now 7:24" at the header's right, so the card says what time it is reading.</param>
/// <param name="StatusBadge">"On time", "Leave now" or "Running late" - the state, named.</param>
/// <param name="LockState">"Locked" or "Unlocked", after the car's name in the footer.</param>
/// <param name="LockAction">The footer's ghost button: "Unlock" when locked, "Lock" when not.</param>
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
    string WeatherLine,
    string NowText,
    string StatusBadge,
    string LockState,
    string LockAction,
    bool IsLocked);

/// <summary>
/// The ETA block and the route ruler. Reached only when there is a trip: no trip means the feed is
/// none, which is what draws the empty state.
/// </summary>
/// <param name="RouteLeave">Where the drive starts along the path, 0 to 1.</param>
/// <param name="RouteArrive">Where the walk starts along the path, 0 to 1.</param>
/// <param name="Slack">The v6 slack ruler's layout; null on a failed lookup.</param>
/// <param name="LeaveCaption">"LEAVE BY", or "LEAVE" once the answer is "Now".</param>
/// <param name="LeaveValue">"7:33", or "Now".</param>
/// <param name="NowCaption">"NOW 7:24" under the ruler's now dot.</param>
/// <param name="SlackSegment">"Slack · 9 min"; the short form is the one word.</param>
/// <param name="RulerDescription">The ruler's one accessible name.</param>
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
    string LeaveCaption,
    string LeaveValue,
    string ArriveCaption,
    string ArriveTime,
    string BellCaption,
    string BellTime,
    string NowCaption,
    string SlackSegment,
    string SlackSegmentShort,
    string DriveSegment,
    string DriveSegmentShort,
    string WalkSegment,
    string RulerDescription,
    string LeaveLabel,
    string ArriveLabel,
    string BellLabel,
    string RouteDescription,
    // Null on a failed lookup, which has no trip to lay on the map or the ruler.
    RulerPlan? Ruler = null,
    SlackRulerPlan? Slack = null);

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
    bool IsStale,
    // The card's one state label, moved down here from the header's pill. It rides this feed
    // because a FeedView template resolves commands through Parent but not state, so the bar has
    // to be handed what it reports rather than reach up for it.
    string StatusBadge,
    CardTone Tone);

