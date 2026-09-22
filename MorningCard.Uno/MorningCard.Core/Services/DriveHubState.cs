using MorningCard.Core.Models;

namespace MorningCard.Core.Services;

/// <summary>
/// Everything the simulator knows, in one value. Both services project from it, so the trip and the
/// car can never disagree about what mode the app is in.
/// </summary>
/// <param name="Revision">
/// Bumped on every change. Two otherwise identical states must not compare equal, or a feed
/// downstream sees no change and keeps the stale result.
/// </param>
public record DriveHubState(
    ConnectionMode Mode,
    int ClockMinutes,
    TripPlan Trip,
    VehicleSnapshot Vehicle,
    bool Faulted,
    long Revision)
{
    /// <summary>7:24, matching the mockup.</summary>
    public const int DefaultClockMinutes = (7 * 60) + 24;

    /// <summary>The time the offline state reports as its last contact.</summary>
    public static readonly TimeOnly LastSeenWhenOffline = new(6, 58);

    public static DriveHubState Default { get; } = new(
        Mode: ConnectionMode.Live,
        ClockMinutes: DefaultClockMinutes,
        Trip: new TripPlan(
            Title: "School drop-off",
            Bell: new TimeOnly(7, 50),
            BaseDriveMin: 14,
            UsualDriveMin: 16,
            DelayMin: 0,
            WalkMin: 3),
        Vehicle: new VehicleSnapshot(
            Name: "XC60",
            IsLocked: true,
            ClimateOn: false,
            CabinTemp: 68,
            Range: 320,
            RangeMax: 400,
            TireFrontLeftPsi: 33,
            IsPluggedIn: false,
            ChargePercent: 0,
            IsStale: false,
            LastSeen: LastSeenWhenOffline),
        Faulted: false,
        Revision: 0);
}
