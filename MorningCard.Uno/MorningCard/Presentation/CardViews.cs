using MorningCard.Core;
using MorningCard.Core.Models;

namespace MorningCard.Presentation;

/// <summary>
/// The three records the card binds to. They are deliberately flat and display-ready: MVUX generates
/// a bindable proxy for every type a feed carries, recursively, so nested records and nullable
/// members are where that generation breaks. Everything here is a string, a number, a bool or an
/// enum, which also keeps the XAML free of converters.
/// </summary>
/// <param name="HeadlineLead">Text before the emphasised span, e.g. "Leave in ".</param>
/// <param name="HeadlineAccent">
/// The span the design emphasises, e.g. "9 min". Split into three parts rather than carrying markup,
/// because the headline is three <c>Run</c>s in one TextBlock and a converter cannot produce Runs.
/// </param>
/// <param name="HeadlineAccentInk">Token key for the emphasis: the tone's ink, or plain ink.</param>
/// <param name="HeadlineAccentSize">27 for the countdown, 17 for an inline time ("leave by 7:33").</param>
/// <param name="ScheduleNote">"Times from your saved schedule." when the live lookup failed.</param>
/// <param name="LockState">"Locked" after the car's name; "Offline · last seen 6:10" when stale.</param>
/// <param name="LockAction">The footer's ghost button: "Unlock" when locked, "Lock" when not.</param>
/// <param name="CanStartDrive">False on a no-trip morning: there is nowhere to drive.</param>
public record HeaderView(
    string Title,
    string HeadlineLead,
    string HeadlineAccent,
    string HeadlineTail,
    string HeadlineAccentInk,
    double HeadlineAccentSize,
    string ScheduleNote,
    bool IsStale,
    string CarName,
    string LockState,
    string LockAction,
    bool IsLocked,
    bool CanStartDrive);

/// <summary>The shell's greeting row: live even while the trip is still loading.</summary>
/// <param name="ClockText">The bare time, "7:24", for the watch's clock row.</param>
public record GreetingView(string Greeting, string WeatherLine, string NowText, string ClockText);

/// <summary>
/// The ETA block and the route ruler. Reached only when there is a trip: no trip means the feed is
/// none, which is what draws the empty state.
/// </summary>
/// <param name="Slack">The v6 slack ruler's layout; null on a failed lookup.</param>
/// <param name="LeaveCaption">"LEAVE BY", or "LEAVE" once the answer is "Now".</param>
/// <param name="LeaveValue">"7:33", or "Now".</param>
/// <param name="NowCaption">"NOW 7:24" under the ruler's now dot.</param>
/// <param name="SlackSegment">"Slack · 9 min"; the short form is the one word.</param>
/// <param name="RulerDescription">The ruler's one accessible name.</param>
public record EtaView(
    bool Failed,
    int TotalMinutes,
    TrafficLevel Traffic,
    string TrafficText,
    string VsUsualText,
    bool IsLate,
    CardTone Tone,
    string LeaveCaption,
    string LeaveValue,
    string ArriveCaption,
    string ArriveTime,
    string BellCaption,
    string BellTime,
    string NowCaption,
    // "NOW" alone, for the watch ring, where the clock already shows the time.
    string NowShortCaption,
    string SlackSegment,
    string SlackSegmentShort,
    string DriveSegment,
    string DriveSegmentShort,
    string WalkSegment,
    string RulerDescription,
    string RouteDescription,
    // The map's two callouts: "Now 7:24" and "3 min walk".
    string MapNowLabel,
    string MapWalkLabel,
    // Null on a failed lookup, which has no trip to lay on the map or the ruler.
    RulerPlan? Ruler = null,
    SlackRulerPlan? Slack = null);

/// <summary>
/// What the map overlay needs from the morning: the timeline (for the slack that places the now
/// dot) and the times it was derived from. The model supplies the times; the view supplies the map.
/// </summary>
public record RulerPlan(TripTimeline Timeline, int WalkMin, TimeOnly Bell, TimeOnly Now);

/// <summary>
/// Wraps the trip so a no-trip morning stays a value on the header's feed. Without it the null
/// becomes a none state and the header loses the car status it still has to show.
/// </summary>
internal record TripSlot(TripPlan? Plan, TripPlan? NextRun = null);

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

/// <summary>The driver's decision on warming the cabin (v6): the row is ready once it is not Pending.</summary>
public enum CabinChoice
{
    Pending,
    Warming,
    Skipped,
}

/// <summary>Which checklist row is open. The phone accordion opens one at a time.</summary>
public enum ChecklistRow
{
    None,
    Fuel,
    Cabin,
    Parking,
}

/// <summary>
/// The checklist under the map: fuel, cabin and parking, each a row with a title, a meta line, a
/// status pill and a detail that opens in place.
///
/// Flat strings on purpose. MVUX generates a bindable proxy for every type a feed carries,
/// recursively, so a list of row records would pull the generator into exactly the shapes it fails
/// on. Three rows is a fixed number the design sets, not a collection. The open flags ride this
/// feed because a FeedView template resolves commands through Parent, but not state.
/// </summary>
public record CarView(
    string ChecksCount,
    string ChecksReady,
    bool ChecksExpanded,
    string FuelTitle,
    string FuelMeta,
    string FuelPill,
    string FuelNoSignal,
    StatLevel FuelLevel,
    bool FuelOffline,
    string FuelDetail,
    bool FuelOpen,
    CabinChoice Cabin,
    string CabinMeta,
    string CabinPill,
    string CabinDetail,
    bool CabinOpen,
    bool CabinPending,
    bool CabinDecided,
    bool CabinSkipped,
    string ParkingTitle,
    string ParkingMeta,
    string ParkingPill,
    string ParkingDetail,
    bool ParkingOpen,
    bool HasDestination,
    bool IsStale,
    // The cabin title greys out once skipped, as well as being struck through.
    string CabinTitleInk,
    // One accessible name per row: title, meta and pill, since the pill is not its own stop.
    string FuelRowName,
    string CabinRowName,
    string ParkingRowName,
    // The watch's cabin line: "Suggested · tap to start" while undecided, else the pill.
    string CabinWatchMeta,
    // The watch's cabin row is the suggestion only while undecided and the car is reachable.
    bool CabinCanStart);
