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

    /// <summary>The drive as geography, for the map on the trip page.</summary>
    ValueTask<TripRoute> GetRouteAsync(CancellationToken ct);

    /// <summary>Re-asks the source, which is also how a failed trip feed recovers.</summary>
    ValueTask RefreshAsync(CancellationToken ct);
}
