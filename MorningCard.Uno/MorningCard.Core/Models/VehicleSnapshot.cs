namespace MorningCard.Core.Models;

/// <summary>
/// One reading of the car. <see cref="IsStale"/> carries the offline case as a value rather than as
/// a feed error, because the design keeps showing the last known data while saying it is old.
///
/// Units are metric throughout (v2, 2026-09-22): <see cref="Range"/> and <see cref="RangeMax"/> are
/// kilometres, every temperature is Celsius. There is no conversion layer - the simulated hub seeds
/// metric and the card formats metric.
/// </summary>
/// <param name="CabinTemp">What the cabin reads now. Cold on a winter morning, which is the point.</param>
/// <param name="CabinTarget">Where climate is heading, so the card can say "warming from x to y".</param>
/// <param name="OutsideTemp">Current outside temperature, shown in the status strip.</param>
/// <param name="WeatherSummary">Conditions plus the day's high, e.g. "Clear, 9°".</param>
public record VehicleSnapshot(
    string Name,
    bool IsLocked,
    bool ClimateOn,
    int CabinTemp,
    int CabinTarget,
    int OutsideTemp,
    string WeatherSummary,
    int Range,
    int RangeMax,
    bool IsPluggedIn,
    int ChargePercent,
    bool IsStale,
    TimeOnly LastSeen);
