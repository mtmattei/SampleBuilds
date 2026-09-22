using Microsoft.Extensions.Time.Testing;
using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Services;
using Xunit;

namespace MorningCard.Tests;

public class SimulatedDriveHubTests
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    private static SimulatedDriveHub NewHub(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 21, 7, 24, 0, TimeSpan.Zero));
        return new SimulatedDriveHub(time) { CommandLatency = TimeSpan.Zero };
    }

    /// <summary>Reads the next element, failing rather than hanging when nothing arrives.</summary>
    private static async Task<T> NextAsync<T>(IAsyncEnumerator<T> enumerator)
    {
        var moved = await enumerator.MoveNextAsync().AsTask().WaitAsync(ReadTimeout);
        Assert.True(moved, "The stream ended before producing a value.");
        return enumerator.Current;
    }

    [Fact]
    public async Task A_new_subscriber_gets_the_current_plan_immediately()
    {
        using var hub = NewHub(out _);

        await using var trips = ((ITripService)hub).Watch(default).GetAsyncEnumerator();

        var plan = await NextAsync(trips);

        Assert.NotNull(plan);
        Assert.Equal("School drop-off", plan!.Title);
        Assert.Equal(new TimeOnly(7, 50), plan.Bell);
    }

    [Fact]
    public async Task Moving_a_knob_pushes_a_new_plan()
    {
        using var hub = NewHub(out _);

        await using var trips = ((ITripService)hub).Watch(default).GetAsyncEnumerator();
        await NextAsync(trips);

        hub.SetTrafficDelay(9);

        var plan = await NextAsync(trips);
        Assert.Equal(9, plan!.DelayMin);
    }

    [Fact]
    public async Task No_trip_mode_pushes_null_so_the_feed_reaches_its_none_state()
    {
        using var hub = NewHub(out _);

        await using var trips = ((ITripService)hub).Watch(default).GetAsyncEnumerator();
        await NextAsync(trips);

        hub.SetMode(ConnectionMode.NoTrip);

        Assert.Null(await NextAsync(trips));
    }

    [Fact]
    public async Task Offline_keeps_serving_the_car_but_marks_it_stale()
    {
        using var hub = NewHub(out _);

        await using var cars = ((IVehicleService)hub).Watch(default).GetAsyncEnumerator();
        var live = await NextAsync(cars);
        Assert.False(live.IsStale);

        hub.SetMode(ConnectionMode.Offline);

        var stale = await NextAsync(cars);
        Assert.True(stale.IsStale);
        Assert.Equal(new TimeOnly(6, 58), stale.LastSeen);

        // The data itself survives: offline is a value, not an absence.
        Assert.Equal(live.Range, stale.Range);
        Assert.Equal(live.Name, stale.Name);
    }

    [Fact]
    public async Task An_injected_fault_breaks_the_trip_feed()
    {
        using var hub = NewHub(out _);
        hub.InjectFault();

        await using var trips = ((ITripService)hub).Watch(default).GetAsyncEnumerator();

        await Assert.ThrowsAsync<CarUnreachableException>(async () => await trips.MoveNextAsync());
    }

    [Fact]
    public async Task The_fault_is_transient_so_resubscribing_is_a_real_retry()
    {
        using var hub = NewHub(out _);
        hub.InjectFault();

        await using (var broken = ((ITripService)hub).Watch(default).GetAsyncEnumerator())
        {
            await Assert.ThrowsAsync<CarUnreachableException>(async () => await broken.MoveNextAsync());
        }

        // This is what the error template's Retry does: it re-requests the feed. If the fault
        // survived, the button would be a guaranteed second failure and worth nothing.
        await using var retried = ((ITripService)hub).Watch(default).GetAsyncEnumerator();
        Assert.NotNull(await NextAsync(retried));
        Assert.False(hub.State.Faulted);
    }

    [Fact]
    public async Task Refreshing_also_clears_a_fault_that_was_never_observed()
    {
        using var hub = NewHub(out _);
        hub.InjectFault();

        await ((ITripService)hub).RefreshAsync(default);

        await using var trips = ((ITripService)hub).Watch(default).GetAsyncEnumerator();
        Assert.NotNull(await NextAsync(trips));
    }

    [Fact]
    public async Task The_last_known_stream_rides_out_a_fault_instead_of_throwing()
    {
        using var hub = NewHub(out _);
        var trip = (ITripService)hub;

        await using var header = trip.WatchLastKnown(default).GetAsyncEnumerator();
        Assert.NotNull(await NextAsync(header));

        hub.InjectFault();

        // No throw: the fault is published like any other change and simply carries no news here.
        Assert.NotNull(await NextAsync(header));

        // The header keeps updating too: the failure belongs to the map panel, which shows it.
        hub.SetTrafficDelay(5);
        var plan = await NextAsync(header);
        Assert.Equal(5, plan!.DelayMin);
    }

    [Fact]
    public async Task Refreshing_brings_a_stale_car_back_to_live()
    {
        using var hub = NewHub(out _);
        hub.SetMode(ConnectionMode.Offline);

        await using var cars = ((IVehicleService)hub).Watch(default).GetAsyncEnumerator();
        Assert.True((await NextAsync(cars)).IsStale);

        await ((IVehicleService)hub).RefreshAsync(default);

        Assert.False((await NextAsync(cars)).IsStale);
        Assert.Equal(ConnectionMode.Live, hub.State.Mode);
    }

    [Fact]
    public async Task The_clock_advances_a_minute_per_minute()
    {
        using var hub = NewHub(out var time);

        await using var clock = ((IVehicleService)hub).WatchClock(default).GetAsyncEnumerator();
        Assert.Equal(new TimeOnly(7, 24), await NextAsync(clock));

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(new TimeOnly(7, 25), await NextAsync(clock));

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(new TimeOnly(7, 26), await NextAsync(clock));
    }

    [Fact]
    public async Task The_clock_dial_sets_the_time_directly()
    {
        using var hub = NewHub(out _);

        await using var clock = ((IVehicleService)hub).WatchClock(default).GetAsyncEnumerator();
        await NextAsync(clock);

        hub.SetClock(TripMath.ToMinutes(new TimeOnly(7, 40)));

        Assert.Equal(new TimeOnly(7, 40), await NextAsync(clock));
    }

    [Fact]
    public async Task The_clock_stream_ignores_changes_that_are_not_the_time()
    {
        using var hub = NewHub(out _);

        await using var clock = ((IVehicleService)hub).WatchClock(default).GetAsyncEnumerator();
        await NextAsync(clock);

        // Range is not the clock; the next clock value must be the one the dial sets.
        hub.SetRange(120);
        hub.SetClock(TripMath.ToMinutes(new TimeOnly(7, 41)));

        Assert.Equal(new TimeOnly(7, 41), await NextAsync(clock));
    }

    [Fact]
    public void Every_change_bumps_the_revision_so_no_value_is_deduped_downstream()
    {
        using var hub = NewHub(out _);

        var before = hub.State;
        hub.SetRange(before.Vehicle.Range);   // same value, deliberately

        var after = hub.State;

        Assert.Equal(before.Vehicle.Range, after.Vehicle.Range);
        Assert.NotEqual(before, after);
        Assert.Equal(before.Revision + 1, after.Revision);
    }

    /// <summary>
    /// The remote commands went with the action row in v2, so lock state now moves only from the
    /// Simulator. The card reports it and cannot change it.
    /// </summary>
    [Fact]
    public void Lock_state_is_driven_by_the_simulator()
    {
        using var hub = NewHub(out _);

        hub.SetLocked(false);

        Assert.False(hub.State.Vehicle.IsLocked);
    }

    [Fact]
    public void Plug_state_and_charge_are_simulated_together()
    {
        using var hub = NewHub(out _);

        hub.SetPluggedIn(true);
        hub.SetChargePercent(140);

        Assert.True(hub.State.Vehicle.IsPluggedIn);
        Assert.Equal(100, hub.State.Vehicle.ChargePercent);
    }

    [Fact]
    public void Range_cannot_be_driven_past_its_maximum()
    {
        using var hub = NewHub(out _);

        hub.SetRange(9_999);

        Assert.Equal(hub.State.Vehicle.RangeMax, hub.State.Vehicle.Range);
    }

}

