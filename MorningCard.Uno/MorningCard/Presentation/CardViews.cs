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
/// <param name="TimesLine">"Leave by 7:33 · Arrive 7:47 · Bell 7:50", empty when there is no trip.</param>
/// <param name="TrafficText">
/// Joins the times line rather than sitting on its own. The v2 mockup dropped the ETA block that
/// used to carry traffic, and the app still computes it, so losing it would have been a silent
/// capability reduction. It keeps its severity colour, which is the part that matters at a glance.
/// </param>
/// <param name="LockText">Status-strip lock line, e.g. "XC60 locked".</param>
public record HeaderView(
    string Greeting,
    string Title,
    string HeadlineLead,
    string HeadlineAccent,
    string HeadlineTail,
    string TimesLine,
    TrafficLevel Traffic,
    string TrafficText,
    CardTone Tone,
    bool IsStale,
    string CarName,
    string LockText,
    string OutsideTemp,
    string WeatherSummary);

/// <summary>
/// The ETA block and the route ruler. Reached only when there is a trip: no trip means the feed is
/// none, which is what draws the empty state.
/// </summary>
/// <param name="RouteLeave">Where the drive starts along the path, 0 to 1.</param>
/// <param name="RouteArrive">Where the walk starts along the path, 0 to 1.</param>
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
public record CarView(
    string FuelValue,
    string FuelUnit,
    string FuelNote,
    StatLevel FuelLevel,
    double FuelPercent,
    string CabinLabel,
    string CabinLead,
    string CabinFrom,
    string CabinMid,
    string CabinTo,
    string DestinationLabel,
    string ParkingStreet,
    string ParkingNote,
    bool HasDestination,
    bool IsStale);

