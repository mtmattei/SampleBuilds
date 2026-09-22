using MorningCard.Core.Models;

namespace MorningCard.Core.Services;

/// <summary>
/// The car: telemetry in. A real implementation would talk to the maker's connected-car API;
/// <see cref="SimulatedDriveHub"/> stands in for it.
///
/// v2 (2026-09-22) removed the remote commands. The redesigned card has no action row, so
/// <c>SetLockedAsync</c>, <c>SetClimateAsync</c> and <c>SendDestinationAsync</c> had no caller and
/// went with it. This is a real capability reduction and is recorded as one in SPEC.md: the card is
/// now read-only, and the Simulator is the only thing that changes the car's state.
/// </summary>
public interface IVehicleService
{
    /// <summary>The current snapshot and every later change, including going stale.</summary>
    IAsyncEnumerable<VehicleSnapshot> Watch(CancellationToken ct);

    /// <summary>Also the clock the card reads, so time can be driven from the simulator.</summary>
    IAsyncEnumerable<TimeOnly> WatchClock(CancellationToken ct);

    /// <summary>Reconnects after the car has gone quiet.</summary>
    ValueTask RefreshAsync(CancellationToken ct);
}
