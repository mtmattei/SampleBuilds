using MorningCard.Core.Models;

namespace MorningCard.Core.Services;

/// <summary>
/// The one route the simulator serves: a short drop-off run through Montreal's West Island. Sample
/// data, not a real address, and the only place in the app that claims a location.
/// </summary>
public static class SampleRoute
{
    public static TripRoute DropOff { get; } = new(
        Home: new GeoPoint(-73.7492, 45.4451),
        School: new GeoPoint(-73.7801, 45.4664),
        Line: new[]
        {
            new GeoPoint(-73.7492, 45.4451),
            new GeoPoint(-73.7521, 45.4478),
            new GeoPoint(-73.7573, 45.4512),
            new GeoPoint(-73.7628, 45.4549),
            new GeoPoint(-73.7684, 45.4583),
            new GeoPoint(-73.7726, 45.4614),
            new GeoPoint(-73.7768, 45.4642),
            new GeoPoint(-73.7801, 45.4664),
        });
}
