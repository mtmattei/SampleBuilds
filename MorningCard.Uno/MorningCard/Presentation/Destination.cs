using Windows.System;

namespace MorningCard.Presentation;

/// <summary>
/// The school the sample drives to, in Montreal's Plateau. A fixed point because the trip is
/// simulated: the sample has no routing service and no location capability, so a moving origin would
/// impersonate one. The map centres here when the route asset is missing, and the maps app opens here.
/// </summary>
public static class Destination
{
    public const double Latitude = 45.5250;

    public const double Longitude = -73.5795;

    /// <summary>A geo: URI, which Android and desktop both hand to the platform maps app.</summary>
    public static Uri MapsUri { get; } = new(FormattableString.Invariant(
        $"geo:{Latitude},{Longitude}?q={Latitude},{Longitude}"));

    /// <summary>Opens the platform maps app at the school. False when no app took the URI.</summary>
    public static async Task<bool> OpenInMapsAsync()
    {
        try
        {
            return await Launcher.LaunchUriAsync(MapsUri);
        }
        catch (Exception error)
        {
            // Nothing to navigate with is not a reason to take the card down; leave a trail instead.
            System.Diagnostics.Debug.WriteLine($"Open in Maps failed for {MapsUri}: {error.GetType().Name}: {error.Message}");
            return false;
        }
    }
}
