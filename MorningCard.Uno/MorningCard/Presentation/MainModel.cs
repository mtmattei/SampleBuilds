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

    /// <summary>The simulated profile's first name (v6 decision F6): mock data, not a feature.</summary>
    private const string ProfileName = "Josh";

    public MainModel(ITripService trip, IVehicleService vehicle, IStringLocalizer text)
    {
        _trip = trip;
        _vehicle = vehicle;
        _text = text;

        // D6: Refresh lives in the Simulator. It signals rather than calling in, because the retry
        // waiter belongs to this model. MainModel lives as long as the app, so the static
        // subscription is never left dangling.
        CardSignals.RefreshRequested += async (_, _) => await Retry(CancellationToken.None);
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

    /// <summary>The greeting row. Only the clock and the car, so it is up before the trip is.</summary>
    public IFeed<GreetingView> Greeting => Feed.Combine(Now, Car).Select(BuildGreeting);

    /// <summary>
    /// Goes none when there is no trip, which is what the map panel's empty state binds to. A fault
    /// upstream surfaces here as the error state.
    /// </summary>
    public IFeed<EtaView> Eta => Feed.Combine(Trip, Now, Slot).Select(BuildEta);

    /// <summary>
    /// The three list rows. Takes the trip as well as the car, because the destination row is a
    /// property of where this trip ends rather than of the vehicle.
    /// </summary>
    public IFeed<CarView> Stats => Feed.Combine(Car, Slot, ChecksExpanded, Now, Cabin, OpenRow).Select(BuildCar);

    /// <summary>The cabin decision (v6): Start warming, Skip, or neither yet.</summary>
    public IState<CabinChoice> Cabin => State.Value(this, () => CabinChoice.Pending);

    /// <summary>The open checklist row. Cabin by default: it is the one the driver can act on.</summary>
    public IState<ChecklistRow> OpenRow => State.Value(this, () => ChecklistRow.Cabin);

    public string EtaUnitLabel => Text("Eta_Unit");

    /// <summary>
    /// "Start drive", or the confirmation while it is showing: "Sent to car", or "Opened in Maps"
    /// when the car is offline and the send was skipped (J6).
    /// </summary>
    public IFeed<string> DriveLabel => Feed.Combine(DriveSent, Car).Select(input =>
        Text(!input.Item1 ? "Drive_Start" : input.Item2.IsStale ? "Drive_Opened" : "Drive_Sent"));

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

    /// <summary>Start warming: records the decision and asks the car for climate.</summary>
    public async ValueTask StartWarming(CancellationToken ct)
    {
        await Cabin.SetAsync(CabinChoice.Warming, ct);
        await _vehicle.SetClimateAsync(true, ct);
    }

    /// <summary>Skip: the cabin stays cold, and the row counts as settled.</summary>
    public async ValueTask SkipWarming(CancellationToken ct) =>
        await Cabin.SetAsync(CabinChoice.Skipped, ct);

    /// <summary>Undo either decision; stops climate if this card started it.</summary>
    public async ValueTask UndoWarming(CancellationToken ct)
    {
        await Cabin.SetAsync(CabinChoice.Pending, ct);

        if (await Car is { ClimateOn: true })
        {
            await _vehicle.SetClimateAsync(false, ct);
        }
    }

    // One command per row: parameters bound from inside a FeedView template do not arrive
    // reliably, so each row names its own.
    public async ValueTask ToggleFuel(CancellationToken ct) => await ToggleRow(ChecklistRow.Fuel, ct);

    public async ValueTask ToggleCabin(CancellationToken ct) => await ToggleRow(ChecklistRow.Cabin, ct);

    public async ValueTask ToggleParking(CancellationToken ct) => await ToggleRow(ChecklistRow.Parking, ct);

    /// <summary>Opening a row closes the other; tapping the open one closes it.</summary>
    private async ValueTask ToggleRow(ChecklistRow row, CancellationToken ct) =>
        await OpenRow.UpdateAsync(open => open == row ? ChecklistRow.None : row, ct);

    /// <summary>
    /// The bottom bar's primary. Sends the drop-off to the car, then hands the driver off to the
    /// maps app - which is the honest meaning of "start drive" in an app that does not navigate.
    /// </summary>
    public async ValueTask StartDrive(CancellationToken ct)
    {
        // J6: with the car offline the send cannot happen, but navigation does not need the car.
        if (await Car is not { IsStale: true })
        {
            await _vehicle.SendDestinationAsync(ct);
        }

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
            yield return new TripSlot(plan, plan is null ? await _trip.GetNextRunAsync(ct).ConfigureAwait(false) : null);
        }
    }

    private GreetingView BuildGreeting((TimeOnly Now, VehicleSnapshot Car) input) => new(
        Greeting: Format("Greeting_Named", ProfileName),
        // A true minus sign (U+2212), as the design sets it, not a hyphen.
        WeatherLine: Format("Weather_Line", OutsideTemp(input.Car), input.Car.WeatherSummary),
        NowText: Format("Status_Now", Clock(input.Now)));

    private static string OutsideTemp(VehicleSnapshot car) =>
        car.OutsideTemp.ToString(CultureInfo.CurrentCulture).Replace('-', '\u2212');

    private HeaderView BuildHeader((TripSlot Slot, TimeOnly Now, VehicleSnapshot Car, bool Unreachable) input)
    {
        var (slot, now, car, unreachable) = input;
        var plan = slot.Plan;
        var timeline = plan is null ? null : TripMath.Derive(plan, now);

        // Three parts so the emphasis can take its own size and ink inline. A no-trip morning still
        // says when tomorrow's run leaves, from the saved schedule.
        var (lead, accent, tail) = timeline is null
            ? slot.NextRun is { } next
                ? (Text("Headline_NoTripLead"), Clock(TripMath.Derive(next, now).LeaveBy), Text("Headline_NoTripTail"))
                : (Text("Headline_NoTrip"), string.Empty, string.Empty)
            : timeline.Tone switch
            {
                CardTone.Late => (
                    Text("Headline_LatePrefix"),
                    Format("Headline_LateAccent", LateByMin(plan!, timeline)),
                    Text("Headline_LateTail")),
                CardTone.LeaveNow => (
                    Text("Headline_LeaveNowPrefix"),
                    Text("Headline_LeaveNowAccent"),
                    Text("Headline_LeaveNowTail")),
                _ => (
                    Text("Headline_LeaveInPrefix"),
                    Format("Headline_LeaveInAccent", timeline.SlackMin),
                    Text("Headline_LeaveInTail")),
            };

        var accentInk = timeline?.Tone switch
        {
            null => "InkBrush",
            CardTone.Late => "DangerInkBrush",
            CardTone.LeaveNow => "LeaveNowInkBrush",
            _ => "AccentInkBrush",
        };

        return new HeaderView(
            Title: plan?.Title ?? Text("Header_NoTripTitle"),
            HeadlineLead: lead,
            HeadlineAccent: accent,
            HeadlineTail: tail,
            HeadlineAccentInk: accentInk,
            HeadlineAccentSize: timeline is null ? 17d : 27d,
            // E6: a failed live lookup still shows the saved schedule, and says so.
            ScheduleNote: unreachable && plan is not null ? Text("Headline_ScheduleNote") : string.Empty,
            Tone: timeline?.Tone ?? CardTone.Ok,
            IsStale: car.IsStale,
            CarName: car.Name,
            // Names the state rather than leaving it to colour alone.
            StatusBadge: car.IsStale
                ? Text("Badge_Offline")
                : timeline is null
                    ? Text("Badge_NoTrip")
                    : Text($"Badge_{timeline.Tone}"),
            LockState: car.IsStale
                ? Format("Lock_Offline", Clock(car.LastSeen))
                : Text(car.IsLocked ? "Lock_Locked" : "Lock_Unlocked"),
            LockAction: Text(car.IsLocked ? "Lock_ActionUnlock" : "Lock_ActionLock"),
            IsLocked: car.IsLocked,
            CanStartDrive: plan is not null);
    }

    /// <summary>Minutes the walk ends after the bell: what "4 min late" counts.</summary>
    private static int LateByMin(TripPlan plan, TripTimeline timeline) =>
        Math.Max(0, TripMath.ToMinutes(timeline.Arrive) + plan.WalkMin - TripMath.ToMinutes(plan.Bell));

    private EtaView BuildEta((TripResult Result, TimeOnly Now, TripSlot Slot) input)
    {
        var (result, now, slot) = input;

        if (result.Plan is not { } plan)
        {
            // E6: the live lookup failed. The saved schedule (no traffic) still places the ruler and
            // the route; only the live ETA block reports the failure.
            return slot.Plan is { } saved
                ? BuildEta((TripResult.Ok(saved with { DelayMin = 0 }), now, slot)) with { Failed = true, FailureMessage = result.FailureMessage }
                : Empty with { Failed = true, FailureMessage = result.FailureMessage };
        }

        var timeline = TripMath.Derive(plan, now);
        var route = RouteLayout.Fractions(timeline, plan.WalkMin);
        var slack = SlackRulerLayout.Compute(timeline, plan.WalkMin, plan.Bell, now);

        var vsUsual = VsUsual(timeline.VsUsualMin);

        var description = timeline.IsLate
            ? Format("Route_AutomationLate",
                Clock(now), -timeline.SlackMin, timeline.DriveMin, Clock(timeline.Arrive), plan.WalkMin, Clock(plan.Bell))
            : Format("Route_AutomationName",
                Clock(now), Clock(timeline.LeaveBy), route.SlackVisibleMin, timeline.DriveMin, Clock(timeline.Arrive), plan.WalkMin, Clock(plan.Bell));

        var rulerName = slack.LateByMin > 0
            ? Format("Ruler_NameLate", Clock(now), timeline.DriveMin, plan.WalkMin, slack.LateByMin, Clock(plan.Bell))
            : slack.LeaveIsNow
                ? Format("Ruler_NameLeaveNow", Clock(now), timeline.DriveMin, plan.WalkMin, Clock(plan.Bell))
                : Format("Ruler_Name", Clock(now), timeline.SlackMin, Clock(timeline.LeaveBy), timeline.DriveMin, plan.WalkMin, Clock(plan.Bell));

        return new EtaView(
            Failed: false,
            FailureMessage: string.Empty,
            TotalMinutes: timeline.DriveMin,
            ArrivalText: Format("Eta_Arrive", ClockWithPeriod(timeline.Arrive)),
            Traffic: timeline.Traffic,
            TrafficText: Text($"Traffic_{timeline.Traffic}"),
            VsUsualText: vsUsual,
            IsLate: timeline.IsLate,
            RouteLeave: route.Leave,
            RouteArrive: route.Arrive,
            Tone: timeline.Tone,
            // Once the answer is "Now" the caption loses its "BY": "LEAVE BY Now" reads wrong.
            LeaveCaption: Text(slack.LeaveIsNow ? "Timeline_Leave" : "Timeline_LeaveBy"),
            LeaveValue: slack.LeaveIsNow ? Text("Timeline_Now") : Clock(timeline.LeaveBy),
            ArriveCaption: Text("Ruler_Arrive"),
            ArriveTime: Clock(timeline.Arrive),
            BellCaption: Text("Ruler_Bell"),
            BellTime: Clock(plan.Bell),
            NowCaption: Format("Ruler_Now", Clock(now)),
            SlackSegment: Format("Ruler_Slack", Math.Max(0, timeline.SlackMin)),
            SlackSegmentShort: Text("Ruler_SlackShort"),
            DriveSegment: Format("Ruler_Drive", timeline.DriveMin),
            DriveSegmentShort: Text("Ruler_DriveShort"),
            WalkSegment: Text("Ruler_Walk"),
            RulerDescription: rulerName,
            LeaveLabel: Format("Route_Leave", Clock(timeline.LeaveBy)),
            ArriveLabel: Format("Route_Arrive", Clock(timeline.Arrive)),
            BellLabel: Format("Route_Bell", Clock(plan.Bell)),
            RouteDescription: description,
            MapNowLabel: Format("Status_Now", Clock(now)),
            MapWalkLabel: Format("Map_Walk", plan.WalkMin),
            Ruler: new RulerPlan(timeline, plan.WalkMin, plan.Bell, now),
            Slack: slack);
    }

    /// <summary>How today compares with a usual morning, shared by the header and the ETA block.</summary>
    private string VsUsual(int vsUsualMin) => vsUsualMin switch
    {
        > 0 => Format("VsUsual_Faster", vsUsualMin),
        < 0 => Format("VsUsual_Slower", -vsUsualMin),
        _ => Text("VsUsual_Same"),
    };

    private CarView BuildCar((VehicleSnapshot Car, TripSlot Slot, bool Expanded, TimeOnly Now, CabinChoice Cabin, ChecklistRow Open) input)
    {
        var (car, slot, expanded, now, choice, open) = input;
        var plan = slot.Plan;
        var timeline = plan is null ? null : TripMath.Derive(plan, now);
        var fuelLevel = TripMath.RangeLevel(car.Range);

        // Climate turned on elsewhere (the Simulator) reads as warming too: the row reports the car.
        var cabin = choice == CabinChoice.Pending && car.ClimateOn ? CabinChoice.Warming : choice;
        var leaveIsNow = timeline is null || timeline.SlackMin <= 0;
        var leave = timeline is null ? string.Empty : Clock(timeline.LeaveBy);
        var cabinFrom = Degrees(car.CabinTemp);
        var cabinTo = Degrees(car.CabinTarget);

        var (cabinMeta, cabinPill, cabinDetail) = cabin switch
        {
            CabinChoice.Warming => (
                leaveIsNow ? Format("Cabin_MetaHeatingNow", cabinFrom, cabinTo) : Format("Cabin_MetaWarmingBy", cabinFrom, cabinTo, leave),
                Text("Cabin_PillWarming"),
                Format("Cabin_DetailWarming", cabinTo)),
            CabinChoice.Skipped => (
                Format("Cabin_MetaSkipped", cabinFrom),
                Text("Cabin_PillSkipped"),
                Text("Cabin_DetailSkipped")),
            _ => (
                Format("Cabin_MetaPending", Degrees(car.OutsideTemp)),
                Text("Cabin_PillSuggested"),
                leaveIsNow ? Format("Cabin_WarmDetail", car.CabinTarget) : Format("Cabin_DetailPendingBy", car.CabinTarget, leave)),
        };

        var fuelPill = fuelLevel switch
        {
            StatLevel.Bad => Text("Fuel_PillNow"),
            StatLevel.Warn => Text("Fuel_PillWeek"),
            _ => Text("Fuel_PillFine"),
        };

        // Fuel and parking are informational, so they are ready by definition; the cabin is ready
        // once the driver has decided either way.
        var ready = 2 + (cabin == CabinChoice.Pending ? 0 : 1);
        var offline = car.IsStale;

        if (offline)
        {
            // What the card last heard, and that the car cannot act on anything now.
            cabinPill = Text("Cabin_PillOffline");
            cabinDetail = Text("Cabin_DetailOffline");
        }

        return new CarView(
            ChecksCount: Format("Checks_Count", 3),
            ChecksReady: plan is null ? Text("Checks_ReadyTomorrow") : Format("Checks_Ready", ready, 3),
            ChecksExpanded: expanded,
            FuelTitle: Format("Fuel_Title", car.Range.ToString(CultureInfo.CurrentCulture)),
            FuelMeta: offline ? Format("Fuel_LastRead", Clock(car.LastSeen)) : Text("Fuel_NoteFine"),
            FuelPill: fuelPill,
            FuelNoSignal: Text("Fuel_PillNoSignal"),
            FuelLevel: fuelLevel,
            FuelOffline: offline,
            FuelDetail: Text(fuelLevel == StatLevel.Normal ? "Fuel_DetailFine" : "Fuel_DetailSoon"),
            FuelOpen: open == ChecklistRow.Fuel,
            Cabin: cabin,
            CabinMeta: cabinMeta,
            CabinPill: cabinPill,
            CabinDetail: cabinDetail,
            CabinOpen: open == ChecklistRow.Cabin,
            CabinPending: cabin == CabinChoice.Pending,
            CabinDecided: cabin != CabinChoice.Pending,
            CabinSkipped: cabin == CabinChoice.Skipped,
            ParkingTitle: plan?.ParkingStreet ?? string.Empty,
            ParkingMeta: plan?.ParkingNote ?? string.Empty,
            ParkingPill: plan is null ? string.Empty : Text("Checks_ParkingChip"),
            ParkingDetail: plan is null ? string.Empty : Format("Parking_Detail", plan.ParkingStreet, plan.ParkingNote),
            ParkingOpen: open == ChecklistRow.Parking,
            // The row leaves entirely on a no-trip morning rather than being drawn empty.
            HasDestination: plan is not null,
            IsStale: car.IsStale,
            StatusBadge: car.IsStale
                ? Text("Badge_Offline")
                : timeline is null
                    ? Text("Badge_NoTrip")
                    : Text($"Badge_{timeline.Tone}"),
            Tone: timeline?.Tone ?? CardTone.Ok,
            FuelRowName: Format("Row_Name", Format("Fuel_Title", car.Range.ToString(CultureInfo.CurrentCulture)), Text("Fuel_NoteFine"), fuelPill),
            CabinRowName: Format("Row_Name", Text("Cabin_Title"), cabinMeta, cabinPill),
            ParkingRowName: plan is null ? string.Empty : Format("Row_Name", plan.ParkingStreet, plan.ParkingNote, Text("Checks_ParkingChip")));
    }

    /// <summary>"−3°", with a true minus sign as the design sets it.</summary>
    private string Degrees(int value) =>
        Format("Temp_Degrees", value.ToString(CultureInfo.CurrentCulture).Replace('-', '\u2212'));

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
        Tone: CardTone.Ok,
        LeaveCaption: string.Empty,
        LeaveValue: string.Empty,
        ArriveCaption: string.Empty,
        ArriveTime: string.Empty,
        BellCaption: string.Empty,
        BellTime: string.Empty,
        NowCaption: string.Empty,
        SlackSegment: string.Empty,
        SlackSegmentShort: string.Empty,
        DriveSegment: string.Empty,
        DriveSegmentShort: string.Empty,
        WalkSegment: string.Empty,
        RulerDescription: string.Empty,
        LeaveLabel: string.Empty,
        ArriveLabel: string.Empty,
        BellLabel: string.Empty,
        RouteDescription: string.Empty,
        MapNowLabel: string.Empty,
        MapWalkLabel: string.Empty);

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



