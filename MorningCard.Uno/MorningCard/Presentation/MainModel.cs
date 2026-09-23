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

    public IFeed<HeaderView> Header => Feed.Combine(Slot, Now, Car).Select(BuildHeader);

    /// <summary>
    /// Goes none when there is no trip, which is what the map panel's empty state binds to. A fault
    /// upstream surfaces here as the error state.
    /// </summary>
    public IFeed<EtaView> Eta => Feed.Combine(Trip, Now).Select(BuildEta);

    /// <summary>
    /// The three list rows. Takes the trip as well as the car, because the destination row is a
    /// property of where this trip ends rather than of the vehicle.
    /// </summary>
    public IFeed<CarView> Stats => Feed.Combine(Car, Slot, ChecksExpanded, Now).Select(BuildCar);

    public string EtaUnitLabel => Text("Eta_Unit");

    /// <summary>"Start drive", or the confirmation while it is showing.</summary>
    public IFeed<string> DriveLabel => DriveSent.Select(sent => Text(sent ? "Drive_Sent" : "Drive_Start"));

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
            var answered = false;

            while (failure is null)
            {
                TripPlan? plan;
                var next = trips.MoveNextAsync().AsTask();

                // The first answer is not in yet, so say so: the view draws its skeleton from this
                // value. Only before the first answer - a later lookup keeps the last ETA on screen.
                // The 300 ms grace keeps a fast answer from flashing the skeleton for one frame.
                if (!answered)
                {
                    await Task.WhenAny(next, Task.Delay(SkeletonGrace, ct)).ConfigureAwait(false);

                    if (!next.IsCompleted)
                    {
                        yield return Option.Some(TripResult.Loading);
                    }
                }

                answered = true;

                try
                {
                    if (!await next.ConfigureAwait(false))
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

    /// <summary>How long a first lookup may take before the Loading skeleton is shown at all.</summary>
    private static readonly TimeSpan SkeletonGrace = TimeSpan.FromMilliseconds(300);

    /// <summary>True for 1.6 seconds after the destination is sent, driving the confirmation.</summary>
    public IState<bool> DriveSent => State.Value(this, () => false);

    /// <summary>
    /// Whether the checklist is open. Starts closed: the header already reports how many checks
    /// there are and how many are settled, which is the whole answer on a morning where nothing
    /// needs doing. Opening it is for the mornings where something does.
    /// </summary>
    public IState<bool> ChecksExpanded => State.Value(this, () => false);

    // ---- commands --------------------------------------------------------------------------

    /// <summary>
    /// Restored in v3 with the checklist and the bottom bar. Each reads the current snapshot rather
    /// than taking the new value as a parameter: parameters bound from inside a FeedView template
    /// do not arrive reliably.
    /// </summary>
    public async ValueTask ToggleLock(CancellationToken ct)
    {
        if (await Car is not { } car)
        {
            return;
        }

        await _vehicle.SetLockedAsync(!car.IsLocked, ct);
    }

    /// <summary>The checklist's "Start warming", and its stop.</summary>
    public async ValueTask ToggleClimate(CancellationToken ct)
    {
        if (await Car is not { } car)
        {
            return;
        }

        await _vehicle.SetClimateAsync(!car.ClimateOn, ct);
    }

    /// <summary>
    /// The bottom bar's primary. Sends the drop-off to the car, then hands the driver off to the
    /// maps app - which is the honest meaning of "start drive" in an app that does not navigate.
    /// </summary>
    public async ValueTask StartDrive(CancellationToken ct)
    {
        await _vehicle.SendDestinationAsync(ct);
        await DriveSent.SetAsync(true, ct);

        DriveStarted?.Invoke(this, EventArgs.Empty);

        // Outside the command on purpose: a generated command holds its button disabled for as long
        // as the method runs, and the confirmation is not something to wait on.
        _ = ResetDriveConfirmationAsync();
    }

    /// <summary>Raised once the destination is away, so the view can open the maps app.</summary>
    public event EventHandler? DriveStarted;

    private async Task ResetDriveConfirmationAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1600));
            await DriveSent.SetAsync(false, CancellationToken.None);
        }
        catch (ObjectDisposedException)
        {
            // The card went away mid-confirmation; there is nothing left to reset.
        }
    }

    /// <summary>Opens or closes the checklist.</summary>
    public async ValueTask ToggleChecks(CancellationToken ct)
    {
        var open = await ChecksExpanded;
        await ChecksExpanded.SetAsync(!open, ct);
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

        // In the bottom bar beside the lock button that changes it, so the state and its control
        // read together.
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
            Tone: timeline?.Tone ?? CardTone.Ok,
            IsStale: car.IsStale,
            CarName: car.Name,
            LockText: lockText,
            IsLocked: car.IsLocked,
            WeatherLine: Format("Status_Weather", Format("Status_Temp", car.OutsideTemp), car.WeatherSummary),
            NowText: Format("Status_Now", Clock(now)),
            LockAction: Text(car.IsLocked ? "Lock_Unlock" : "Lock_Lock"));
    }

    private EtaView BuildEta((TripResult Result, TimeOnly Now) input)
    {
        var (result, now) = input;

        if (result.Pending)
        {
            return Empty with { IsLoading = true };
        }

        if (result.Plan is not { } plan)
        {
            return Empty with { Failed = true, FailureMessage = result.FailureMessage };
        }

        var timeline = TripMath.Derive(plan, now);
        var route = RouteLayout.Fractions(timeline, plan.WalkMin);

        var (trafficText, trafficNote) = TrafficLine(timeline, plan, now);

        var description = timeline.IsLate
            ? Format("Route_AutomationLate",
                Clock(now), -timeline.SlackMin, timeline.DriveMin, Clock(timeline.Arrive), plan.WalkMin, Clock(plan.Bell))
            : Format("Route_AutomationName",
                Clock(now), Clock(timeline.LeaveBy), route.SlackVisibleMin, timeline.DriveMin, Clock(timeline.Arrive), plan.WalkMin, Clock(plan.Bell));

        return new EtaView(
            Failed: false,
            IsLoading: false,
            ShowsRoute: true,
            FailureMessage: string.Empty,
            DriveMinutes: timeline.DriveMin,
            Traffic: timeline.Traffic,
            TrafficText: trafficText,
            VsUsualText: trafficNote,
            IsLate: timeline.IsLate,
            RouteLeave: route.Leave,
            RouteArrive: route.Arrive,
            Tone: timeline.Tone,
            // Once the answer is "Now" the column header loses its "BY": "LEAVE BY Now" reads wrong.
            LeaveByTime: timeline.Tone == CardTone.Ok ? Clock(timeline.LeaveBy) : Text("Timeline_Now"),
            LeaveByLabel: Text(timeline.Tone == CardTone.Ok ? "Timeline_LeaveBy" : "Timeline_Leave"),
            ArriveTime: Clock(timeline.Arrive),
            BellTime: Clock(plan.Bell),
            // A floor on each, so no segment collapses to nothing and the track keeps its shape
            // even when the slack is gone or the walk is a single minute.
            TrackSlack: Math.Max(route.Leave, 0.04),
            TrackDrive: Math.Max(route.Arrive - route.Leave, 0.04),
            TrackWalk: Math.Max(1d - route.Arrive, 0.04),
            LeaveLabel: Format("Route_Leave", Clock(timeline.LeaveBy)),
            RouteDescription: description,
            Ruler: new RulerPlan(timeline, plan.WalkMin, plan.Bell, now));
    }

    /// <summary>
    /// The traffic line under the drive time, written for the state the card is in. On a normal
    /// morning it compares the drive with a usual one; once the driver is late it says where the
    /// lateness comes from, because "Light traffic" beside "Running late" reads as a contradiction.
    /// </summary>
    private (string Text, string Note) TrafficLine(TripTimeline timeline, TripPlan plan, TimeOnly now)
    {
        if (!timeline.IsLate)
        {
            return (Text($"Traffic_{timeline.Traffic}"), VsUsual(timeline.VsUsualMin));
        }

        return timeline.Traffic == TrafficLevel.Light
            ? (Text("Traffic_LateClear"), Format("Traffic_LateStart", Clock(now)))
            : (Text($"Traffic_{timeline.Traffic}"), Format("Traffic_LateDelay", plan.DelayMin));
    }

    /// <summary>How today compares with a usual morning.</summary>
    private string VsUsual(int vsUsualMin) => vsUsualMin switch
    {
        > 0 => Format("VsUsual_Faster", vsUsualMin),
        < 0 => Format("VsUsual_Slower", -vsUsualMin),
        _ => Text("VsUsual_Same"),
    };

    private CarView BuildCar((VehicleSnapshot Car, TripSlot Slot, bool Expanded, TimeOnly Now) input)
    {
        var (car, slot, expanded, now) = input;
        var fuelLevel = TripMath.RangeLevel(car.Range);
        var timeline = slot.Plan is null ? null : TripMath.Derive(slot.Plan, now);

        // Two clauses: whether today's driving fits, and whether it needs attention soon. The
        // second half is the one that carries the colour, so it travels separately.
        var fuelNote = fuelLevel switch
        {
            StatLevel.Bad => Text("Fuel_NoteLowAccent"),
            StatLevel.Warn => Text("Fuel_NoteSoonAccent"),
            _ => string.Empty,
        };

        // Climate off means the cabin is not going anywhere, so the row states the temperature
        // rather than inventing a target it is not heading for.
        var cabinLead = car.ClimateOn ? Text("Cabin_WarmingLead") : Text("Cabin_RestingLead");

        return new CarView(
            FuelValue: car.Range.ToString(CultureInfo.CurrentCulture),
            FuelUnit: Text("Fuel_Unit"),
            FuelNote: Text("Fuel_NoteFine"),
            FuelNoteAccent: fuelNote,
            FuelChip: fuelNote.TrimStart(' ', '\u00b7', ' '),
            FuelLevel: fuelLevel,
            FuelTitle: Text(fuelLevel == StatLevel.Bad ? "Fuel_TitleLow" : "Fuel_NoteFine"),
            CabinLabel: Text("Cabin_Label"),
            CabinLead: cabinLead,
            // Reports the climate command the Simulator holds; the card can no longer send one.
            CabinAccent: car.ClimateOn ? Text("Cabin_RemoteOn") : string.Empty,
            ChecksCount: Format("Checks_Count", 3),
            // Fuel and parking are informational, so they are ready by definition; the cabin is the
            // one the driver can still act on, which is what "2 of 3" is counting.
            ChecksReady: Format("Checks_Ready", car.ClimateOn ? 3 : 2, 3),
            CabinNeedsWarming: !car.ClimateOn,
            ChecksExpanded: expanded,
            ParkingChip: slot.Plan is null ? string.Empty : Format("Checks_ParkingChip", slot.Plan.ParkingNote),
            DestinationLabel: slot.Plan?.DestinationLabel ?? string.Empty,
            ParkingStreet: slot.Plan?.ParkingStreet ?? string.Empty,
            ParkingNote: slot.Plan?.ParkingNote ?? string.Empty,
            // The row leaves entirely on a no-trip morning. Kept, it rendered a wrapped label, an
            // empty value column and a chevron pointing at nothing.
            HasDestination: slot.Plan is not null,
            IsStale: car.IsStale,
            Tone: timeline?.Tone ?? CardTone.Ok);
    }

    /// <summary>A blank view to build a failure on, so every member stays non-null.</summary>
    private static EtaView Empty { get; } = new(
        Failed: false,
        IsLoading: false,
        ShowsRoute: false,
        FailureMessage: string.Empty,
        DriveMinutes: 0,
        Traffic: TrafficLevel.Light,
        TrafficText: string.Empty,
        VsUsualText: string.Empty,
        IsLate: false,
        RouteLeave: 0d,
        RouteArrive: 1d,
        Tone: CardTone.Ok,
        LeaveByTime: string.Empty,
        LeaveByLabel: string.Empty,
        ArriveTime: string.Empty,
        BellTime: string.Empty,
        TrackSlack: 1d,
        TrackDrive: 1d,
        TrackWalk: 1d,
        LeaveLabel: string.Empty,
        RouteDescription: string.Empty);

    private string Text(string key) => _text[key]!;

    private string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, _text[key]!, args);

    /// <summary>
    /// A bare clock time for the map labels: the culture's short-time pattern with the AM/PM
    /// designator removed. Seven labels each carrying "AM" crowds the panel and says nothing on a
    /// card that only ever shows one morning.
    /// A 24-hour culture is unaffected, since its pattern has no designator to strip.
    /// </summary>
    private static string Clock(TimeOnly time) =>
        time.ToString(BareTimePattern, CultureInfo.CurrentCulture);

    private static readonly string BareTimePattern =
        CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern
            .Replace("tt", string.Empty, StringComparison.Ordinal)
            .Replace("t", string.Empty, StringComparison.Ordinal)
            .Trim();

}



