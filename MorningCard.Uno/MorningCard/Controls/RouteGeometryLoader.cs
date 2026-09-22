using MorningCard.Core.Route;
using Windows.Storage;

namespace MorningCard.Controls;

/// <summary>
/// Loads the committed route asset, once per run.
/// </summary>
/// <remarks>
/// The route does not change with the clock or the theme, so there is nothing to reload: only the
/// times laid over it move. The task is cached the same way the packaged typeface is.
/// </remarks>
internal static class RouteGeometryLoader
{
    private static Task<RouteGeometry?>? _load;

    /// <summary>
    /// Underscores only, no hyphens. Android rewrites a hyphen in a Content asset filename to an
    /// underscore, so a hyphenated path resolves on desktop and fails silently on the phone.
    /// </summary>
    private const string AssetPath = "ms-appx:///Assets/route.json";

    public static Task<RouteGeometry?> LoadAsync() => _load ??= LoadCoreAsync();

    private static async Task<RouteGeometry?> LoadCoreAsync()
    {
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(AssetPath));
            var json = await FileIO.ReadTextAsync(file);

            var geometry = RouteAsset.TryParse(json);

            if (geometry is null)
            {
                DebugTrace("route asset parsed to nothing; the map keeps its tiles and no route.");
            }

            return geometry;
        }
        catch (Exception error)
        {
            // A missing or unreadable asset must not take the card down, but it must not be silent
            // either: without this the map would simply show no route and look like a layout bug.
            DebugTrace($"route asset load failed: {error.GetType().Name}: {error.Message}");
            return null;
        }
    }

    /// <summary>The whole trip's extent, for fitting the map to it.</summary>
    public static Mapsui.MRect Bounds(RouteGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var leg in new[] { geometry.Drive, geometry.Walk })
        {
            foreach (var point in leg.Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        var width = Math.Max(maxX - minX, 1d);
        var height = Math.Max(maxY - minY, 1d);

        // Room for the furniture that hangs off the end pins - a dwell ring at 22px radius and a
        // callout about 70px wide. All of it is screen-constant while this box is in metres, and
        // the resolution is not known until the fit happens, so the padding is expressed as the
        // fraction of the route's width that lands roughly 60px at the width the panel fits. Six
        // percent was not enough and clipped the origin's callout against the right edge.
        var pad = width * 0.13d;

        // A little top padding, to keep the route off the attribution line along the bottom. It used
        // to be four times this, pushing the route into the lower band to dodge the ETA text - but
        // the scrim is opaque now and the gutter below clears that text horizontally, so the extra
        // drop only served to park the origin pin underneath the Open in Maps button.
        var headroom = width * 0.10d;

        // A small left gutter as well, since the ETA block reaches furthest on that side.
        var etaGutter = width * 1.05d;

        return new Mapsui.MRect(
            minX - pad - etaGutter,
            minY - pad,
            maxX + pad,
            maxY + pad + headroom);
    }

    private static void DebugTrace(string message)
    {
        System.Diagnostics.Debug.WriteLine($"RouteGeometryLoader: {message}");

        var path = Environment.GetEnvironmentVariable("MC_TRACE");

        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                File.AppendAllText(path, $"RouteGeometryLoader: {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // A trace that cannot be written must not take the app with it.
            }
        }
    }
}
