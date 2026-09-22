using MorningCard.Core.Models;

namespace MorningCard.Core.Services;

/// <summary>
/// The morning's trip. A real implementation would combine the calendar with a routing service;
/// <see cref="SimulatedDriveHub"/> stands in for both.
/// </summary>
public interface ITripService
{
    /// <summary>
    /// Pushes the current plan and every later change. A null element means there is no trip today,
    /// which is how the feed reaches its none state.
    /// </summary>
    IAsyncEnumerable<TripPlan?> Watch(CancellationToken ct);

    /// <summary>
    /// The same stream without the failures: it reports what is known and stops rather than
    /// throwing. The header uses it so a failed trip lookup does not also blank the car status,
    /// which the map panel's error state is already reporting.
    /// </summary>
    IAsyncEnumerable<TripPlan?> WatchLastKnown(CancellationToken ct);

    /// <summary>Re-asks the source, which is also how a failed trip feed recovers.</summary>
    ValueTask RefreshAsync(CancellationToken ct);
}
