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
    public IFeed<EtaView> Eta => Feed.Combine(Trip, Now).Select(BuildEta);

    public IFeed<CarView> Stats => Car.Select(BuildCar);

    /// <summary>True for 1.4 seconds after a destination is sent, driving the "Sent" label.</summary>
    public IState<bool> SendConfirmed => State.Value(this, () => false);

    private IFeed<TripResult> Trip => Feed<TripResult>.AsyncEnumerable(WatchTrip);

    /// <summary>
    /// Reads the trip, and owns reconnecting after a failure.
    ///
    /// A service that cannot reach the car throws, which is the honest contract; the stream it was
    /// feeding dies with it. So the failure is turned into a value, and the loop then waits for the
    /// user's retry before opening a fresh subscription. Nothing else can restart it: refreshing
    /// the feed does not re-subscribe a stream that has thrown.
    ///
    /// A null plan stays an explicit none, which is what draws the empty state.
    /// </summary>
    private async IAsyncEnumerable<Option<TripResult>> WatchTrip([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var trips = _trip.Watch(ct).GetAsyncEnumerator(ct);
            string? failure = null;

            while (failure is null)
            {
                TripPlan? plan;

                try
                {
                    if (!await trips.MoveNextAsync().ConfigureAwait(false))
                    {
                        yield break;
                    }

                    plan = trips.Current;
                }
                catch (CarUnreachableException error)
                {
                    failure = error.Message;
                    break;
                }

                yield return plan is null
                    ? Option.None<TripResult>()
                    : Option.Some(TripResult.Ok(plan));
            }

            yield return Option.Some(TripResult.Failure(failure));

            await WaitForRetryAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>Blocks the trip loop until <see cref="Retry"/> runs.</summary>
    private Task WaitForRetryAsync(CancellationToken ct)
    {
        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _retryWaiter, waiter);
        ct.Register(() => waiter.TrySetResult());
        return waiter.Task;
    }

    private TaskCompletionSource? _retryWaiter;

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

        // Releases the trip loop, which then opens a fresh subscription.
        Interlocked.Exchange(ref _retryWaiter, null)?.TrySetResult();
    }

    // ---- projections -----------------------------------------------------------------------

    private async IAsyncEnumerable<TripSlot> WatchSlot([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var plan in _trip.WatchLastKnown(ct).ConfigureAwait(false))
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
            // Not "No drop-off today": the map panel already says that, and a card should not
            // deliver the same sentence twice.
            Title: plan?.Title ?? Text("Header_NoTripTitle"),
            Headline: headline,
            Tone: timeline?.Tone ?? CardTone.Ok,
            HasTrip: plan is not null,
            IsStale: car.IsStale,
            CarName: car.Name,
            CarStatus: status);
    }

    private EtaView BuildEta((TripResult Result, TimeOnly Now) input)
    {
        var (result, now) = input;

        if (result.Plan is not { } plan)
        {
            return Empty with { Failed = true, FailureMessage = result.FailureMessage };
        }

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
            Failed: false,
            FailureMessage: string.Empty,
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

    /// <summary>A blank view to build a failure on, so every member stays non-null.</summary>
    private static EtaView Empty { get; } = new(
        Failed: false,
        FailureMessage: string.Empty,
        TotalMinutes: 0,
        ArrivalText: string.Empty,
        Traffic: TrafficLevel.Light,
        TrafficText: string.Empty,
        VsUsualText: string.Empty,
        IsLate: false,
        RouteLeave: 0d,
        RouteArrive: 1d,
        SlackVisibleMinutes: 0,
        NowLabel: string.Empty,
        LeaveLabel: string.Empty,
        SlackLabel: string.Empty,
        DriveLabel: string.Empty,
        ArriveLabel: string.Empty,
        WalkLabel: string.Empty,
        BellLabel: string.Empty,
        RouteDescription: string.Empty);

    private string Text(string key) => _text[key]!;

    private string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, _text[key]!, args);

    private static string Clock(TimeOnly time) => time.ToString("H:mm", CultureInfo.InvariantCulture);

    private static string ClockWithPeriod(TimeOnly time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

}
