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

    /// <summary>
    /// Whether the last trip lookup failed. A state, not the trip feed: the header has to keep
    /// speaking on a no-trip morning, and Feed.Combine fast-starts to None as soon as any parent is
    /// None - combining with the trip feed took the entire header off the card. A state always has
    /// a value, so it can join the combine safely.
    /// </summary>
    private IState<bool> Unreachable => State.Value(this, () => false);

    public IFeed<HeaderView> Header => Feed.Combine(Slot, Now, Car, Unreachable).Select(BuildHeader);

    /// <summary>
    /// Goes none when there is no trip, which is what the map panel's empty state binds to. A fault
    /// upstream surfaces here as the error state.
    /// </summary>
    public IFeed<EtaView> Eta => Feed.Combine(Trip, Now).Select(BuildEta);

    /// <summary>
    /// The three list rows. Takes the trip as well as the car, because the destination row is a
    /// property of where this trip ends rather than of the vehicle.
    /// </summary>
    public IFeed<CarView> Stats => Feed.Combine(Car, Slot).Select(BuildCar);

    public string EtaUnitLabel => Text("Eta_Unit");

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

                await Unreachable.SetAsync(false, ct).ConfigureAwait(false);

                yield return plan is null
                    ? Option.None<TripResult>()
                    : Option.Some(TripResult.Ok(plan));
            }

            await Unreachable.SetAsync(true, ct).ConfigureAwait(false);

            yield return Option.Some(TripResult.Failure(failure));

            await WaitForRetryAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Blocks the trip loop until <see cref="Retry"/> runs.
    ///
    /// The cancellation registration is disposed in the finally: it used to be discarded, so one
    /// registration accumulated on the feed's long-lived token per failure-and-retry cycle and was
    /// only released when the whole subscription ended.
    /// </summary>
    private async Task WaitForRetryAsync(CancellationToken ct)
    {
        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _retryWaiter, waiter);

        var registration = ct.Register(() => waiter.TrySetResult());

        try
        {
            await waiter.Task.ConfigureAwait(false);
        }
        finally
        {
            registration.Dispose();
        }
    }

    private TaskCompletionSource? _retryWaiter;

    // ---- commands --------------------------------------------------------------------------

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

    private HeaderView BuildHeader((TripSlot Slot, TimeOnly Now, VehicleSnapshot Car, bool Unreachable) input)
    {
        var (slot, now, car, unreachable) = input;
        var plan = slot.Plan;
        var timeline = plan is null ? null : TripMath.Derive(plan, now);

        // Three parts so the duration can take the accent colour inline, which is what the v2
        // mockup draws. Only the middle Run is coloured; an empty accent collapses to plain text.
        var (lead, accent, tail) = car.IsStale
            ? (Format("Headline_Offline", Clock(car.LastSeen)), string.Empty, string.Empty)
            : timeline is null
                ? (Text("Headline_NoTrip"), string.Empty, string.Empty)
                : timeline.Tone switch
                {
                    CardTone.Late => (
                        Text("Headline_LatePrefix"),
                        Format("Headline_LateAccent", -timeline.SlackMin),
                        Format("Headline_LateTail", Clock(timeline.Arrive))),
                    CardTone.LeaveNow => (
                        Text("Headline_LeaveNowPrefix"),
                        Text("Headline_LeaveNowAccent"),
                        Text("Headline_LeaveNowTail")),
                    _ => (
                        Text("Headline_LeaveInPrefix"),
                        Format("Headline_LeaveInAccent", timeline.SlackMin),
                        Text("Headline_LeaveInTail")),
                };

        // The strip reports lock state; nothing on the card changes it any more (v2 decision A).
        var lockText = car.IsStale
            ? Format("CarStatus_LastSeen", Clock(car.LastSeen))
            : Format(car.IsLocked ? "Status_Locked" : "Status_Unlocked", car.Name);

        return new HeaderView(
            Greeting: Text("Greeting"),
            // Not "No drop-off today": the map panel already says that, and a card should not
            // deliver the same sentence twice.
            Title: plan?.Title ?? Text("Header_NoTripTitle"),
            HeadlineLead: lead,
            HeadlineAccent: accent,
            HeadlineTail: tail,
            // Gone while the trip lookup is failing. Stating five specifics as current fact beside
            // "Can't reach the car" left the user no way to tell which half was true. The headline
            // stays, from last known data, which is one claim rather than five.
            TimesLine: timeline is null || plan is null || unreachable
                ? string.Empty
                : Format("Header_Times", Clock(timeline.LeaveBy), Clock(timeline.Arrive), Clock(plan.Bell)),
            Traffic: timeline?.Traffic ?? TrafficLevel.Light,
            // Carries its own leading separator: composed here rather than as a literal Run, because
            // adjacent Runs are joined with the whitespace between them in the markup.
            TrafficText: timeline is null || unreachable
                ? string.Empty
                : Format("Header_TrafficClause", Text($"Traffic_{timeline.Traffic}")),
            Tone: timeline?.Tone ?? CardTone.Ok,
            IsStale: car.IsStale,
            CarName: car.Name,
            LockText: lockText,
            OutsideTemp: Format("Status_Temp", car.OutsideTemp),
            WeatherSummary: car.WeatherSummary);
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

        var vsUsual = VsUsual(timeline.VsUsualMin);

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

    /// <summary>How today compares with a usual morning, shared by the header and the ETA block.</summary>
    private string VsUsual(int vsUsualMin) => vsUsualMin switch
    {
        > 0 => Format("VsUsual_Faster", vsUsualMin),
        < 0 => Format("VsUsual_Slower", -vsUsualMin),
        _ => Text("VsUsual_Same"),
    };

    private CarView BuildCar((VehicleSnapshot Car, TripSlot Slot) input)
    {
        var (car, slot) = input;
        var fuelLevel = TripMath.RangeLevel(car.Range);

        // Two clauses: whether today's driving fits, and whether it needs attention soon. The
        // mockup joins them with a middot, and the second half is what turns the icon amber.
        var fuelNote = fuelLevel switch
        {
            StatLevel.Bad => Text("Fuel_NoteLow"),
            StatLevel.Warn => Text("Fuel_NoteSoon"),
            _ => Text("Fuel_NoteFine"),
        };

        // Climate off means the cabin is not going anywhere, so the row states the temperature
        // rather than inventing a target it is not heading for.
        var cabinLead = car.ClimateOn ? Text("Cabin_WarmingLead") : Text("Cabin_RestingLead");

        return new CarView(
            FuelValue: car.Range.ToString(CultureInfo.CurrentCulture),
            FuelUnit: Text("Fuel_Unit"),
            FuelNote: fuelNote,
            FuelLevel: fuelLevel,
            FuelPercent: TripMath.RangeBarPercent(car.Range, car.RangeMax),
            CabinLabel: Text("Cabin_Label"),
            CabinLead: cabinLead,
            CabinFrom: Format("Temp_Degrees", car.CabinTemp),
            CabinMid: car.ClimateOn ? Text("Cabin_To") : string.Empty,
            CabinTo: car.ClimateOn ? Format("Temp_Degrees", car.CabinTarget) : string.Empty,
            DestinationLabel: slot.Plan?.DestinationLabel ?? string.Empty,
            ParkingStreet: slot.Plan?.ParkingStreet ?? string.Empty,
            ParkingNote: slot.Plan?.ParkingNote ?? string.Empty,
            // The row leaves entirely on a no-trip morning. Kept, it rendered a wrapped label, an
            // empty value column and a chevron pointing at nothing.
            HasDestination: slot.Plan is not null,
            IsStale: car.IsStale);
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

    /// <summary>
    /// A bare clock time for the map labels: the culture's short-time pattern with the AM/PM
    /// designator removed. Seven labels each carrying "AM" crowds the panel and says nothing on a
    /// card that only ever shows one morning - the designator belongs to the arrival line alone.
    /// A 24-hour culture is unaffected, since its pattern has no designator to strip.
    /// </summary>
    private static string Clock(TimeOnly time) =>
        time.ToString(BareTimePattern, CultureInfo.CurrentCulture);

    /// <summary>The arrival line, where the designator earns its place.</summary>
    private static string ClockWithPeriod(TimeOnly time) => time.ToString("t", CultureInfo.CurrentCulture);

    private static readonly string BareTimePattern =
        CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern
            .Replace("tt", string.Empty, StringComparison.Ordinal)
            .Replace("t", string.Empty, StringComparison.Ordinal)
            .Trim();

}



