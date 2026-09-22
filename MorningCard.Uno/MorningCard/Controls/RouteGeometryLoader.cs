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

        // Framed the way the reference frames it: the route sits across the middle of the panel,
        // roughly from three tenths to three quarters. These two numbers are the solution to
        // putting the route's own extent in that window - asymmetric because the scrim owns the
        // left of the panel and the route has no business under the ETA block.
        var leftPad = width * 0.67d;
        var rightPad = width * 0.56d;

        // A little at the top, to keep the route off the attribution line along the bottom.
        var headroom = width * 0.10d;

        return new Mapsui.MRect(
            minX - leftPad,
            minY - rightPad,
            maxX + rightPad,
            maxY + rightPad + headroom);
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
