using MorningCard.Themes;
using Windows.Foundation;
using MorningCard.Core.Route;
using Windows.System;

namespace MorningCard.Controls;

/// <summary>
/// The card. It inherits the page's DataContext (the generated MainViewModel) and has no model of
/// its own: there is one card, and splitting its state from the page would only add a hop.
/// </summary>
public sealed partial class MorningCardView : UserControl
{
    /// <summary>
    /// The drop-off, in Montreal's Plateau. A fixed point because the trip itself is simulated:
    /// the sample has no routing service and no location capability, so inventing a moving origin
    /// would be impersonating one.
    /// </summary>
    private const double DestinationLatitude = 45.5250;

    private const double DestinationLongitude = -73.5795;

    public MorningCardView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CardChrome.Changed += OnChromeChanged;
        ApplyChrome();

        _ = ShowMapAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => CardChrome.Changed -= OnChromeChanged;

    private void OnChromeChanged(object? sender, EventArgs e) => ApplyChrome();

    /// <summary>
    /// Puts OSM tiles under the route, then fits and locks the map to the route it has to carry.
    ///
    /// Since v4 the route is real geometry rather than a schematic path, so the map is no longer a
    /// textured backdrop: the two have to agree. The map is pan- and zoom-locked because the card
    /// shows one fixed trip and the ruler's tick interval is chosen once, for the resolution the
    /// route is fitted at.
    /// </summary>
    private async Task ShowMapAsync()
    {
        try
        {
            var map = new Mapsui.Map();

            // OSM blocks the default agent outright, and the block arrives as a tile image reading
            // "Access blocked" rather than as an error - so it renders as map content and looks
            // like a layout bug. Their usage policy wants an agent that identifies the app.
            map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer(
                "MorningCard/1.0 (Uno Platform sample; https://github.com/mtmattei/SampleBuilds)"));

            Map.Map = map;
            Ruler.Map = Map;

            var geometry = await RouteGeometryLoader.LoadAsync();

            if (geometry is null)
            {
                // No asset means no route to fit to, so the map keeps a plain view of the school
                // and the ruler draws nothing. The card's other rows still have something to say.
                var fallback = Mapsui.Projections.SphericalMercator.FromLonLat(
                    DestinationLongitude, DestinationLatitude);

                map.Navigator.CenterOnAndZoomTo(new Mapsui.MPoint(fallback.x, fallback.y), 4);
                return;
            }

            FitToRoute(map, geometry);

            Ruler.Geometry = geometry;
            Ruler.ChooseInterval();
        }
        catch (Exception error)
        {
            // A map that cannot start must not take the card with it: everything else on the card
            // still has something to say, and the panel keeps its own background.
            DebugTrace($"map init failed: {error.GetType().Name}: {error.Message}");
        }
    }

    /// <summary>
    /// Frames the whole trip, then stops the map moving.
    /// </summary>
    /// <remarks>
    /// The padding is asymmetric on purpose: the ETA block sits over the left of the panel behind
    /// its scrim, so the route is pushed clear of it rather than drawn underneath and then covered.
    /// </remarks>
    private static void FitToRoute(Mapsui.Map map, RouteGeometry geometry)
    {
        var bounds = RouteGeometryLoader.Bounds(geometry);

        map.Navigator.ZoomToBox(bounds, Mapsui.MBoxFit.Fit);

        // Locked after the fit, never before: a locked navigator ignores the fit as well.
        map.Navigator.PanLock = true;
        map.Navigator.ZoomLock = true;
        map.Navigator.RotationLock = true;
    }

    /// <summary>
    /// Opens the platform maps app at the drop-off.
    ///
    /// Wired to a real launch rather than left as a decorative pill: a control that looks tappable
    /// and does nothing is the failure mode the capability rule exists to prevent.
    /// </summary>
    private async void OnOpenInMaps(object sender, RoutedEventArgs e)
    {
        var uri = new Uri(FormattableString.Invariant(
            $"geo:{DestinationLatitude},{DestinationLongitude}?q={DestinationLatitude},{DestinationLongitude}"));

        try
        {
            await Launcher.LaunchUriAsync(uri);
        }
        catch (Exception error)
        {
            DebugTrace($"Open in Maps failed for {uri}: {error.GetType().Name}: {error.Message}");
        }
    }

    private static void DebugTrace(string message)
    {
#if DEBUG
        DebugHooks.Trace(message);
#endif
    }

    /// <summary>
    /// The radius dial writes here rather than into a theme resource: a ThemeResource only
    /// re-resolves on a theme change, so an already-bound CornerRadius would never move.
    /// </summary>
    private void ApplyChrome()
    {
        var radius = CardChrome.CornerRadius;
        Frame.CornerRadius = new CornerRadius(radius);
    }
}
