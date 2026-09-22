using MorningCard.Core.Models;

namespace MorningCard.Core.Services;

/// <summary>
/// The car: telemetry in, remote commands out. A real implementation would talk to the maker's
/// connected-car API; <see cref="SimulatedDriveHub"/> stands in for it.
///
/// The commands were removed in v2, when the card had no action row, and restored in v3, which puts
/// them back as checklist actions and a bottom bar. Each one takes latency and can be refused while
/// the car is unreachable, because that is what makes the pending and disabled states worth having.
/// </summary>
public interface IVehicleService
{
    /// <summary>The current snapshot and every later change, including going stale.</summary>
    IAsyncEnumerable<VehicleSnapshot> Watch(CancellationToken ct);

    /// <summary>Also the clock the card reads, so time can be driven from the simulator.</summary>
    IAsyncEnumerable<TimeOnly> WatchClock(CancellationToken ct);

    /// <summary>Locks or unlocks. Takes visible time, so the button has a pending state.</summary>
    ValueTask SetLockedAsync(bool locked, CancellationToken ct);

    /// <summary>Starts or stops pre-conditioning, which is what "Start warming" drives.</summary>
    ValueTask SetClimateAsync(bool on, CancellationToken ct);

    /// <summary>Sends the drop-off to the car's navigation, behind "Start drive".</summary>
    ValueTask SendDestinationAsync(CancellationToken ct);

    /// <summary>Reconnects after the car has gone quiet.</summary>
    ValueTask RefreshAsync(CancellationToken ct);
}
