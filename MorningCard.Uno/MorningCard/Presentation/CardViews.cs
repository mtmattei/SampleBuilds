using MorningCard.Core.Models;

namespace MorningCard.Presentation;

/// <summary>
/// The three records the card binds to. They are deliberately flat and display-ready: MVUX generates
/// a bindable proxy for every type a feed carries, recursively, so nested records and nullable
/// members are where that generation breaks. Everything here is a string, a number, a bool or an
/// enum, which also keeps the XAML free of converters.
/// </summary>
/// <param name="Headline">
/// The one sentence the card exists to deliver, already resolved for late, leave-now, no-trip and
/// unreachable.
/// </param>
public record HeaderView(
    string Greeting,
    string Title,
    string Headline,
    CardTone Tone,
    bool HasTrip,
    bool IsStale,
    string CarName,
    string CarStatus);

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

/// <summary>The stats row and the four actions.</summary>
public record CarView(
    string RangeValue,
    string RangeUnit,
    string RangeCaption,
    StatLevel RangeLevel,
    double RangePercent,
    string ChargeValue,
    string ChargeCaption,
    bool IsCharging,
    string TireValue,
    string TireCaption,
    StatLevel TireLevel,
    bool IsLocked,
    bool ClimateOn,
    string LockLabel,
    string ClimateValue,
    bool IsStale);
