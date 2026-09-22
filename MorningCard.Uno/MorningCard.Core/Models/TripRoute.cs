namespace MorningCard.Core.Models;

/// <summary>A WGS84 point. Longitude first, matching the order map libraries expect.</summary>
public record GeoPoint(double Longitude, double Latitude);

/// <summary>
/// The drive as geography, for the map on the trip page. Sample data: the card's ruler is a
/// schematic and does not use these coordinates.
/// </summary>
public record TripRoute(GeoPoint Home, GeoPoint School, IReadOnlyList<GeoPoint> Line);
