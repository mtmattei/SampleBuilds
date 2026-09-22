using MorningCard.Themes;
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

        ShowMap();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => CardChrome.Changed -= OnChromeChanged;

    private void OnChromeChanged(object? sender, EventArgs e) => ApplyChrome();

    /// <summary>
    /// Puts OSM tiles under the route.
    ///
    /// The route drawn on top stays schematic - minutes become distance along a path - so this map
    /// is a textured ground rather than the route's geometry. That is a deliberate trade recorded in
    /// SPEC.md v2: it keeps the time-as-distance device the card is built around, at the cost of a
    /// drawn line that does not follow the streets beneath it.
    /// </summary>
    private void ShowMap()
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

            // Mapsui 5 dropped Map.Home; the navigator is the entry point, and it only answers once
            // the control has a size - hence after the assignment.
            var centre = Mapsui.Projections.SphericalMercator.FromLonLat(DestinationLongitude, DestinationLatitude);
            map.Navigator.CenterOnAndZoomTo(new Mapsui.MPoint(centre.x, centre.y), 4);
        }
        catch (Exception error)
        {
            // A map that cannot start must not take the card with it: everything else on the card
            // still has something to say, and the panel keeps its own background.
            DebugTrace($"map init failed: {error.GetType().Name}: {error.Message}");
        }
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

    /// <summary>
    /// Replays the route's entrance. It lives in the view because the animation is the view's, not
    /// the model's - the Simulator raises the same signal.
    /// </summary>
    private void OnReplayEntrance(object sender, RoutedEventArgs e) => CardMotion.RequestReplay();

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
        Glass.CornerRadius = new CornerRadius(radius);
    }
}
