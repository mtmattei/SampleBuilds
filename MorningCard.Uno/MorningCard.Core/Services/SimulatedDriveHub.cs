using System.Runtime.CompilerServices;
using MorningCard.Core.Models;

namespace MorningCard.Core.Services;

/// <summary>
/// Stands in for the routing service and the connected car. Nothing here talks to a network: the
/// simulator panel writes to this hub, and the card reads the resulting feeds, which is the same
/// path real services would take. Swapping in real ones is a registration change.
/// </summary>
public sealed class SimulatedDriveHub : ITripService, IVehicleService, IDisposable
{
    private readonly Broadcast<DriveHubState> _state = new(DriveHubState.Default);
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _clockCts = new();
    private readonly Task _clockLoop;

    public SimulatedDriveHub(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _clockLoop = RunClockAsync(_clockCts.Token);
    }

    /// <summary>
    /// How long a remote command takes to come back. Real enough to show a pending state; tests set
    /// it to zero.
    /// </summary>
    public TimeSpan CommandLatency { get; set; } = TimeSpan.FromMilliseconds(600);

    public DriveHubState State => _state.Current;

    // ---- simulator knobs -------------------------------------------------------------------

    public void SetMode(ConnectionMode mode) => Mutate(state => state with
    {
        Mode = mode,
        // Leaving a broken state through the mode chips is a deliberate escape hatch.
        Faulted = false,
        Vehicle = state.Vehicle with
        {
            IsStale = mode == ConnectionMode.Offline,
            LastSeen = mode == ConnectionMode.Offline
                ? DriveHubState.LastSeenWhenOffline
                : TripMath.FromMinutes(state.ClockMinutes),
        },
    });

    public void SetClock(int minutesSinceMidnight) => Mutate(state => state with
    {
        ClockMinutes = Wrap(minutesSinceMidnight),
    });

    public void SetTrafficDelay(int delayMin) => Mutate(state => state with
    {
        Trip = state.Trip with { DelayMin = Math.Max(0, delayMin) },
    });

    public void SetRange(int rangeMi) => Mutate(state => state with
    {
        Vehicle = state.Vehicle with { Range = Math.Clamp(rangeMi, 0, state.Vehicle.RangeMax) },
    });

    public void SetCabinTemp(int degrees) => Mutate(state => state with
    {
        Vehicle = state.Vehicle with { CabinTemp = degrees },
    });

    public void SetOutsideTemp(int degrees) => Mutate(state => state with
    {
        Vehicle = state.Vehicle with { OutsideTemp = degrees },
    });

    /// <summary>
    /// The card shows lock state in its status strip but can no longer change it, so the Simulator
    /// owns the only way to move it. Direct, not awaited: this is the simulator, not the car.
    /// </summary>
    public void SetLocked(bool locked) => Mutate(state => state with
    {
        Vehicle = state.Vehicle with { IsLocked = locked },
    });

    public void SetPluggedIn(bool pluggedIn) => Mutate(state => state with
    {
        Vehicle = state.Vehicle with { IsPluggedIn = pluggedIn },
    });

    public void SetChargePercent(int percent) => Mutate(state => state with
    {
        Vehicle = state.Vehicle with { ChargePercent = Math.Clamp(percent, 0, 100) },
    });

    /// <summary>
    /// Breaks the next trip lookup so the error template and its retry can be driven end to end.
    /// The failure is transient: it is consumed by the attempt that trips on it, so retrying
    /// succeeds. That is what makes a retry worth offering in the first place.
    /// </summary>
    public void InjectFault() => Mutate(state => state with { Faulted = true });

    // ---- ITripService ----------------------------------------------------------------------

    public async IAsyncEnumerable<TripPlan?> Watch([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var state in _state.Watch(ct).ConfigureAwait(false))
        {
            if (state.Faulted)
            {
                // Consumed on the way out, so the retry that follows is a real attempt rather than
                // a second guaranteed failure.
                Mutate(current => current with { Faulted = false });
                throw new CarUnreachableException("Could not reach the car to plan this morning's trip.");
            }

            yield return state.Mode == ConnectionMode.NoTrip ? null : state.Trip;
        }
    }

    public async IAsyncEnumerable<TripPlan?> WatchLastKnown([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var state in _state.Watch(ct).ConfigureAwait(false))
        {
            // A fault says the lookup failed, not that the known plan changed, so it is ignored here.
            yield return state.Mode == ConnectionMode.NoTrip ? null : state.Trip;
        }
    }

    ValueTask ITripService.RefreshAsync(CancellationToken ct) => ReconnectAsync(ct);

    // ---- IVehicleService -------------------------------------------------------------------

    async IAsyncEnumerable<VehicleSnapshot> IVehicleService.Watch([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var state in _state.Watch(ct).ConfigureAwait(false))
        {
            yield return state.Vehicle;
        }
    }

    public async IAsyncEnumerable<TimeOnly> WatchClock([EnumeratorCancellation] CancellationToken ct)
    {
        var last = -1;

        await foreach (var state in _state.Watch(ct).ConfigureAwait(false))
        {
            // Every knob republishes the whole state; only the minute changing is news here.
            if (state.ClockMinutes == last)
            {
                continue;
            }

            last = state.ClockMinutes;
            yield return TripMath.FromMinutes(state.ClockMinutes);
        }
    }

    public async ValueTask SetLockedAsync(bool locked, CancellationToken ct)
    {
        await DelayAsync(ct).ConfigureAwait(false);
        Mutate(state => state with { Vehicle = state.Vehicle with { IsLocked = locked } });
    }

    public async ValueTask SetClimateAsync(bool on, CancellationToken ct)
    {
        await DelayAsync(ct).ConfigureAwait(false);
        Mutate(state => state with { Vehicle = state.Vehicle with { ClimateOn = on } });
    }

    public async ValueTask SendDestinationAsync(CancellationToken ct)
    {
        await DelayAsync(ct).ConfigureAwait(false);

        // Nothing on the car changes; the confirmation lives in the view.
        Mutate(state => state);
    }

    ValueTask IVehicleService.RefreshAsync(CancellationToken ct) => ReconnectAsync(ct);

    // ---- internals -------------------------------------------------------------------------

    private async ValueTask ReconnectAsync(CancellationToken ct)
    {
        await DelayAsync(ct).ConfigureAwait(false);

        Mutate(state => state with
        {
            Mode = ConnectionMode.Live,
            Faulted = false,
            Vehicle = state.Vehicle with
            {
                IsStale = false,
                LastSeen = TripMath.FromMinutes(state.ClockMinutes),
            },
        });
    }

    private ValueTask DelayAsync(CancellationToken ct) => CommandLatency <= TimeSpan.Zero
        ? ValueTask.CompletedTask
        : new ValueTask(Task.Delay(CommandLatency, _timeProvider, ct));

    private void Mutate(Func<DriveHubState, DriveHubState> mutate) =>
        _state.Mutate(state => mutate(state) with { Revision = state.Revision + 1 });

    /// <summary>
    /// Advances the simulated clock one minute per real minute, so slack genuinely shrinks while the
    /// app is open rather than only when the dial moves.
    /// </summary>
    private async Task RunClockAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), _timeProvider);

            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                Mutate(state => state with { ClockMinutes = Wrap(state.ClockMinutes + 1) });
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    private static int Wrap(int minutes) =>
        ((minutes % TripMath.MinutesPerDay) + TripMath.MinutesPerDay) % TripMath.MinutesPerDay;

    public void Dispose()
    {
        _clockCts.Cancel();
        _clockCts.Dispose();
    }
}
