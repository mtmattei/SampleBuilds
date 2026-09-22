namespace MorningCard.Core.Models;

/// <summary>
/// One reading of the car. <see cref="IsStale"/> carries the offline case as a value rather than as
/// a feed error, because the design keeps showing the last known data while saying it is old.
/// </summary>
public record VehicleSnapshot(
    string Name,
    bool IsLocked,
    bool ClimateOn,
    int CabinTemp,
    int Range,
    int RangeMax,
    int TireFrontLeftPsi,
    bool IsPluggedIn,
    int ChargePercent,
    bool IsStale,
    TimeOnly LastSeen);
