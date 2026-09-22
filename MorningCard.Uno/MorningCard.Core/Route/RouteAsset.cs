using System.Text.Json;
using System.Text.Json.Serialization;

namespace MorningCard.Core.Route;

/// <summary>
/// One leg as it sits in the committed asset: the shape as lon/lat pairs, plus OSRM's per-edge
/// annotations. Both annotation arrays have one entry fewer than <see cref="Coordinates"/>.
/// </summary>
public sealed class RouteLegAsset
{
    [JsonPropertyName("coordinates")]
    public double[][] Coordinates { get; set; } = [];

    [JsonPropertyName("durations")]
    public double[] Durations { get; set; } = [];

    [JsonPropertyName("distances")]
    public double[] Distances { get; set; } = [];
}

/// <summary>The asset file: the drive, then the walk.</summary>
public sealed class RouteAssetDocument
{
    [JsonPropertyName("drive")]
    public RouteLegAsset? Drive { get; set; }

    [JsonPropertyName("walk")]
    public RouteLegAsset? Walk { get; set; }
}

/// <summary>
/// Source-generated metadata for the asset. Core is trimmable, and the reflection-based serializer
/// cannot promise these types survive trimming - the generated context can.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RouteAssetDocument))]
internal sealed partial class RouteAssetContext : JsonSerializerContext;

/// <summary>
/// Reads the committed route asset into a <see cref="RouteGeometry"/>.
///
/// The asset is generated once, offline, from OSRM with <c>annotations=duration,distance</c>, and
/// checked in. There is no routing service in the app and no live traffic: the durations are
/// free-flow and describe the shape of the speed profile, which reconciliation then stretches to
/// whatever the card is currently saying the drive takes.
/// </summary>
public static class RouteAsset
{
    /// <summary>
    /// Parses the asset. Returns null when it is missing, malformed, or describes a drive with no
    /// shape - the caller falls back to pins and the strip rather than taking the card down.
    /// </summary>
    public static RouteGeometry? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        RouteAssetDocument? document;

        try
        {
            document = JsonSerializer.Deserialize(json, RouteAssetContext.Default.RouteAssetDocument);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document?.Drive is null)
        {
            return null;
        }

        var drive = BuildLeg(RouteMode.Drive, document.Drive);

        // A route with no drive has nothing to draw. A route with no walk is a curbside drop-off,
        // which is a real shape, so an empty walk leg is built rather than rejected.
        if (drive is null || drive.Edges.Count == 0)
        {
            return null;
        }

        var walk = BuildLeg(RouteMode.Walk, document.Walk) ?? EmptyLegAt(drive.Points[^1]);

        return new RouteGeometry(drive, walk);
    }

    /// <summary>
    /// Turns one asset leg into a <see cref="RouteLeg"/>, projecting to Spherical Mercator and
    /// dropping zero-length edges.
    /// </summary>
    /// <remarks>
    /// Zero-length edges come out of OSRM wherever it repeats a coordinate at a junction. They
    /// carry no ground and would sit in the cumulative arrays as points a tick can never usefully
    /// land on, so they go here rather than being special-cased in the layout.
    /// </remarks>
    private static RouteLeg? BuildLeg(RouteMode mode, RouteLegAsset? asset)
    {
        if (asset is null || asset.Coordinates.Length < 2)
        {
            return null;
        }

        var edgeCount = asset.Coordinates.Length - 1;

        if (asset.Durations.Length < edgeCount || asset.Distances.Length < edgeCount)
        {
            return null;
        }

        var points = new List<RoutePoint>(asset.Coordinates.Length);
        var edges = new List<RouteEdge>(edgeCount);

        var first = asset.Coordinates[0];

        if (first.Length < 2)
        {
            return null;
        }

        points.Add(Project(first[0], first[1]));

        for (var i = 0; i < edgeCount; i++)
        {
            var next = asset.Coordinates[i + 1];

            if (next.Length < 2)
            {
                return null;
            }

            var length = asset.Distances[i];

            if (length <= 0d)
            {
                continue;
            }

            points.Add(Project(next[0], next[1]));
            edges.Add(new RouteEdge(length, asset.Durations[i]));
        }

        if (edges.Count == 0)
        {
            return null;
        }

        return new RouteLeg(mode, points, edges);
    }

    /// <summary>A leg that goes nowhere, anchored where the previous one ended.</summary>
    private static RouteLeg EmptyLegAt(RoutePoint point) =>
        new(RouteMode.Walk, [point], []);

    /// <summary>
    /// Spherical Mercator (EPSG:3857) forward projection, in metres.
    ///
    /// Written out rather than taken from Mapsui so that this project stays free of the map
    /// library: the layout and its tests have no reason to load a rendering stack.
    /// </summary>
    internal static RoutePoint Project(double longitude, double latitude)
    {
        const double EarthRadius = 6378137d;

        // Clamped to the Mercator limit, past which the projection runs to infinity.
        var clamped = Math.Clamp(latitude, -85.05112878d, 85.05112878d);

        var x = EarthRadius * (longitude * Math.PI / 180d);
        var y = EarthRadius * Math.Log(Math.Tan((Math.PI / 4d) + (clamped * Math.PI / 360d)));

        return new RoutePoint(x, y);
    }
}
