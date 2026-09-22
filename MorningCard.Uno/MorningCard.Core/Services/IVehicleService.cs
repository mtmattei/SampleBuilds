using MorningCard.Core.Models;

namespace MorningCard.Core.Services;

/// <summary>
/// The car: telemetry in, remote commands out. A real implementation would talk to the maker's
/// connected-car API; <see cref="SimulatedDriveHub"/> stands in for it.
/// </summary>
public interface IVehicleService
{
    /// <summary>The current snapshot and every later change, including going stale.</summary>
    IAsyncEnumerable<VehicleSnapshot> Watch(CancellationToken ct);

    /// <summary>Also the clock the card reads, so time can be driven from the simulator.</summary>
    IAsyncEnumerable<TimeOnly> WatchClock(CancellationToken ct);

    ValueTask SetLockedAsync(bool locked, CancellationToken ct);

    ValueTask SetClimateAsync(bool on, CancellationToken ct);

    /// <summary>Sends the drop-off destination to the car's navigation.</summary>
    ValueTask SendDestinationAsync(CancellationToken ct);

    /// <summary>Reconnects after the car has gone quiet.</summary>
    ValueTask RefreshAsync(CancellationToken ct);
}
