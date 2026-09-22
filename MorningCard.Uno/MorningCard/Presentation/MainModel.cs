using System.Runtime.CompilerServices;
using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Services;

namespace MorningCard.Presentation;

/// <summary>
/// The card's state. Three feeds, one per region of the card, so each can show its own progress and
/// none state: the header always has something to say, the ETA block goes none on a day with no
/// drop-off, and the stats row keeps reporting the car either way.
/// </summary>
public partial record MainModel
{
    private readonly ITripService _trip;
    private readonly IVehicleService _vehicle;
    private readonly IStringLocalizer _text;

    public MainModel(ITripService trip, IVehicleService vehicle, IStringLocalizer text)
    {
        _trip = trip;
        _vehicle = vehicle;
        _text = text;
    }

    /// <summary>The simulated clock. Advances a minute per minute, and the dial can set it.</summary>
    public IFeed<TimeOnly> Now => Feed.AsyncEnumerable(_vehicle.WatchClock);

    /// <summary>The car. Present even when stale, which is how the offline state keeps its data.</summary>
    public IFeed<VehicleSnapshot> Car => Feed.AsyncEnumerable(_vehicle.Watch);

    /// <summary>
    /// The trip, or none on a day without one. Carried in a slot so the null survives the feed:
    /// the header still has to say something on a no-trip morning.
    /// </summary>
    private IFeed<TripSlot> Slot => Feed.AsyncEnumerable(WatchSlot);

    public IFeed<HeaderView> Header => Feed.Combine(Slot, Now, Car).Select(BuildHeader);

    /// <summary>
    /// Goes none when there is no trip, which is what the map panel's empty state binds to. A fault
    /// upstream surfaces here as the error state.
    /// </summary>
    public IFeed<EtaView> Eta => Feed.Combine(Plan, Now).Select(BuildEta);

    public IFeed<CarView> Stats => Car.Select(BuildCar);

    /// <summary>True for 1.4 seconds after a destination is sent, driving the "Sent" label.</summary>
    public IState<bool> SendConfirmed => State.Value(this, () => false);

    private IFeed<TripPlan> Plan => Feed<TripPlan>.AsyncEnumerable(WatchPlan);

    /// <summary>
    /// Turns the service's null into an explicit none, which is what puts the map panel into its
    /// empty state. A plain nullable would not satisfy the feed's notnull constraint.
    /// </summary>
    private async IAsyncEnumerable<Option<TripPlan>> WatchPlan([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var plan in _trip.Watch(ct).ConfigureAwait(false))
        {
            yield return plan is null ? Option.None<TripPlan>() : Option.Some(plan);
        }
    }

    // ---- commands --------------------------------------------------------------------------

    // Awaiting a feed yields nothing while it is still loading, so both toggles read the current
    // snapshot rather than taking the new value as a command parameter: parameters bound from
    // inside a FeedView template do not arrive reliably.
    public async ValueTask ToggleLock(CancellationToken ct)
    {
        if (await Car is not { } car)
        {
            return;
        }

        await _vehicle.SetLockedAsync(!car.IsLocked, ct);
    }

    public async ValueTask ToggleClimate(CancellationToken ct)
    {
        if (await Car is not { } car)
        {
            return;
        }

        await _vehicle.SetClimateAsync(!car.ClimateOn, ct);
    }

    public async ValueTask SendToCar(CancellationToken ct)
    {
        await _vehicle.SendDestinationAsync(ct);
        await SendConfirmed.SetAsync(true, ct);

        // The 1.4s reset runs outside the command: a generated command holds its button disabled for
        // as long as the method runs, and "Sent" is a confirmation, not a wait.
        _ = ResetSendConfirmationAsync();
    }

    public async ValueTask Retry(CancellationToken ct)
    {
        await _vehicle.RefreshAsync(ct);
        await _trip.RefreshAsync(ct);
    }

    // ---- projections -----------------------------------------------------------------------

    private async IAsyncEnumerable<TripSlot> WatchSlot([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var plan in _trip.Watch(ct).ConfigureAwait(false))
        {
            yield return new TripSlot(plan);
        }
    }

    private HeaderView BuildHeader((TripSlot Slot, TimeOnly Now, VehicleSnapshot Car) input)
    {
        var (slot, now, car) = input;
        var plan = slot.Plan;
        var timeline = plan is null ? null : TripMath.Derive(plan, now);

        var headline = car.IsStale
            ? Format("Headline_Offline", Clock(car.LastSeen))
            : timeline is null
                ? Text("Headline_NoTrip")
                : timeline.Tone switch
                {
                    CardTone.Late => Format("Headline_Late", -timeline.SlackMin, Clock(timeline.Arrive)),
                    CardTone.LeaveNow => Text("Headline_LeaveNow"),
                    _ => Format("Headline_LeaveIn", timeline.SlackMin),
                };

        var status = car.IsStale
            ? Format("CarStatus_LastSeen", Clock(car.LastSeen))
            : Format("CarStatus_Line", Text(car.IsLocked ? "CarStatus_Locked" : "CarStatus_Unlocked"), car.CabinTemp);

        return new HeaderView(
            Greeting: Text("Greeting"),
            Title: plan?.Title ?? Text("Empty_Title"),
            Headline: headline,
            Tone: timeline?.Tone ?? CardTone.Ok,
            HasTrip: plan is not null,
            IsStale: car.IsStale,
            CarName: car.Name,
            CarStatus: status);
    }

    private EtaView BuildEta((TripPlan Plan, TimeOnly Now) input)
    {
        var (plan, now) = input;
        var timeline = TripMath.Derive(plan, now);
        var route = RouteLayout.Fractions(timeline, plan.WalkMin);

        var vsUsual = timeline.VsUsualMin switch
        {
            > 0 => Format("VsUsual_Faster", timeline.VsUsualMin),
            < 0 => Format("VsUsual_Slower", -timeline.VsUsualMin),
            _ => Text("VsUsual_Same"),
        };

        var description = timeline.IsLate
            ? Format("Route_AutomationLate",
                Clock(now), -timeline.SlackMin, timeline.DriveMin, Clock(timeline.Arrive), plan.WalkMin, Clock(plan.Bell))
            : Format("Route_AutomationName",
                Clock(now), Clock(timeline.LeaveBy), route.SlackVisibleMin, timeline.DriveMin, Clock(timeline.Arrive), plan.WalkMin, Clock(plan.Bell));

        return new EtaView(
            TotalMinutes: timeline.TotalMin,
            ArrivalText: Format("Eta_Arrive", ClockWithPeriod(timeline.Arrive)),
            Traffic: timeline.Traffic,
            TrafficText: Text($"Traffic_{timeline.Traffic}"),
            VsUsualText: vsUsual,
            IsLate: timeline.IsLate,
            RouteLeave: route.Leave,
            RouteArrive: route.Arrive,
            SlackVisibleMinutes: route.SlackVisibleMin,
            NowLabel: Format("Route_Now", Clock(now)),
            LeaveLabel: Format("Route_Leave", Clock(timeline.LeaveBy)),
            SlackLabel: Format("Route_Slack", route.SlackVisibleMin),
            DriveLabel: Format("Route_Drive", timeline.DriveMin),
            ArriveLabel: Format("Route_Arrive", Clock(timeline.Arrive)),
            WalkLabel: Format("Route_Walk", plan.WalkMin),
            BellLabel: Format("Route_Bell", Clock(plan.Bell)),
            RouteDescription: description);
    }

    private CarView BuildCar(VehicleSnapshot car)
    {
        var tireLevel = TripMath.TireLevel(car.TireFrontLeftPsi);

        var tireValue = tireLevel switch
        {
            StatLevel.Bad => Format("Stat_TirePsi", car.TireFrontLeftPsi),
            StatLevel.Warn => Text("Stat_TireLow"),
            _ => Text("Stat_TireAllGood"),
        };

        var chargeCaption = !car.IsPluggedIn
            ? Text("Stat_ChargeNotCharging")
            : car.ChargePercent >= 100
                ? Text("Stat_ChargeFull")
                : Text("Stat_ChargeCharging");

        return new CarView(
            RangeValue: car.Range.ToString(CultureInfo.CurrentCulture),
            RangeUnit: Text("Stat_RangeUnit"),
            RangeCaption: Text("Stat_RangeCaption"),
            RangeLevel: TripMath.RangeLevel(car.Range),
            RangePercent: TripMath.RangeBarPercent(car.Range, car.RangeMax),
            ChargeValue: car.IsPluggedIn ? Format("Stat_ChargeValue", car.ChargePercent) : Text("Stat_ChargeNone"),
            ChargeCaption: chargeCaption,
            IsCharging: car.IsPluggedIn,
            TireValue: tireValue,
            TireCaption: Text("Stat_TireCaption"),
            TireLevel: tireLevel,
            IsLocked: car.IsLocked,
            ClimateOn: car.ClimateOn,
            LockLabel: Text(car.IsLocked ? "Action_Lock" : "Action_Unlock"),
            ClimateValue: car.ClimateOn
                ? Format("Action_ClimateOn", car.CabinTemp)
                : Format("Action_ClimateOff", car.CabinTemp),
            IsStale: car.IsStale);
    }

    private async Task ResetSendConfirmationAsync()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1400));
        await SendConfirmed.SetAsync(false, CancellationToken.None);
    }

    private string Text(string key) => _text[key]!;

    private string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, _text[key]!, args);

    private static string Clock(TimeOnly time) => time.ToString("H:mm", CultureInfo.InvariantCulture);

    private static string ClockWithPeriod(TimeOnly time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

}
