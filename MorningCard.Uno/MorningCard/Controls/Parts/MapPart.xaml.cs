using Microsoft.UI.Xaml.Media.Animation;
using MorningCard.Core.Route;
using MorningCard.Presentation;
using MorningCard.Themes;
using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>
/// The route map part: tiles, overlay, ETA block and Open in Maps. Shared by the phone card, the TV
/// page and the watch; the host sets its size and the fit follows whatever it is given.
/// </summary>
public sealed partial class MapPart : UserControl
{
    public static readonly DependencyProperty ShowMapsPillProperty = DependencyProperty.Register(
        nameof(ShowMapsPill), typeof(bool), typeof(MapPart), new PropertyMetadata(true));

    private bool _started;

    /// <summary>False on the watch, which puts Open in Maps under the map as its own button (v6 Wear).</summary>
    public bool ShowMapsPill
    {
        get => (bool)GetValue(ShowMapsPillProperty);
        set => SetValue(ShowMapsPillProperty, value);
    }

    public MapPart()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // xaml-lint: allow codebehind - lifecycle: starts the Mapsui map once, no XAML surface
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again after a re-parent; the map is built once.
        if (_started)
        {
            return;
        }

        _started = true;
        _ = ShowMapAsync();
    }

    /// <summary>
    /// Puts OSM tiles under the route, then fits and locks the map to the route it has to carry.
    ///
    /// Since v4 the route is real geometry rather than a schematic path, so the map is no longer a
    /// textured backdrop: the two have to agree. The map is pan- and zoom-locked (B4, I6) because
    /// the card shows one fixed trip; Open in Maps is the way out to a live map.
    /// </summary>
    private async Task ShowMapAsync()
    {
        try
        {
            var map = new Mapsui.Map();

            // OSM blocks the default agent outright, and the block arrives as a tile image reading
            // "Access blocked" rather than as an error - so it renders as map content and looks
            // like a layout bug. Their usage policy wants an agent that identifies the app.
            //
            // v6: the tiles are muted toward the paper map (spike R8). MutedTileSource re-renders
            // each tile once through a colour matrix; Mapsui caches the result.
            var osm = Mapsui.Tiling.OpenStreetMap.CreateTileLayer(
                "MorningCard/1.0 (Uno Platform sample; https://github.com/mtmattei/SampleBuilds)");
            var paper = Themes.CardPalette.Lookup("MapColor") is Windows.UI.Color c
                ? new SkiaSharp.SKColor(c.R, c.G, c.B)
                : new SkiaSharp.SKColor(0xEF, 0xEE, 0xE9);
            map.Layers.Add(new Mapsui.Tiling.Layers.TileLayer(new MutedTileSource(
                (BruTile.IHttpTileSource)osm.TileSource, MutedTileSource.PaperMatrix(paper))));

            Map.Map = map;
            Ruler.Map = Map;

            var geometry = await RouteGeometryLoader.LoadAsync();

            if (geometry is null)
            {
                // No asset means no route to fit to, so the map keeps a plain view of the school
                // and the ruler draws nothing. The card's other rows still have something to say.
                var fallback = Mapsui.Projections.SphericalMercator.FromLonLat(
                    Destination.Longitude, Destination.Latitude);

                map.Navigator.CenterOnAndZoomTo(new Mapsui.MPoint(fallback.x, fallback.y), 4);
                return;
            }

            // The fit waits for the viewport to have a size: on a slow start (the emulator) the route
            // arrived first, the navigator ignored the fit, and the route never drew. The viewport
            // reporting a size is the signal.
            _route = geometry;
            _fittedSize = default;
            _viewportReady = false;
            Ruler.Geometry = geometry;
            map.Navigator.ViewportChanged += OnViewportChanged;
            FitToRoute();
        }
        catch (Exception error)
        {
            // A map that cannot start must not take the card with it: everything else on the card
            // still has something to say, and the panel keeps its own background.
            DebugTrace($"map init failed: {error.GetType().Name}: {error.Message}");
        }
    }

    // Screen-space room around the route, in viewport units. The ETA block and its fade own the
    // left of the panel, the dwell ring (22 radius) hangs off the right end, the callouts sit above
    // the pins and the Open in Maps pill and attribution take the bottom band. These used to be
    // fractions of the route's width, which shrank with the panel: at 411 epx the ring clipped on
    // the right and the route slid under the fade.
    private const double FitLeftShare = 0.40d;

    // Clear map between the ETA text and the first pin: the Bell callout is centred on its pin and
    // reaches about this far left of it.
    private const double FitEtaGap = 40d;

    // The scrim's fade after the ETA text, and the ramp across it (fractions of the fade, stops 2-8):

    private double _etaEdge;

    /// <summary>
    /// Moves the route's left edge to wherever the ETA text actually ends, so the fit keeps the
    /// route clear of it. The scrim is a fixed ramp since v6.
    /// </summary>
    // xaml-lint: allow codebehind - measured ETA text edge bounds the Mapsui route fit (map-control API)
    private void OnEtaBlockSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not FrameworkElement block)
        {
            return;
        }

        var edge = block.Margin.Left + e.NewSize.Width;

        if (Math.Abs(edge - _etaEdge) < 0.5d)
        {
            return;
        }

        _etaEdge = edge;

        _fittedSize = default;
        FitToRoute();
    }
    private const double FitRight = 30d;
    private const double FitTop = 34d;
    private const double FitBottom = 50d;

    private RouteGeometry? _route;
    private Size _fittedSize;

    private bool _viewportReady;

    // xaml-lint: allow codebehind - Mapsui navigator event: refits the locked map on resize (map-control API)
    private void OnViewportChanged(object? sender, EventArgs e) => FitToRoute();

    /// <summary>
    /// Frames the whole trip inside the part of the panel nothing covers, then stops the map moving.
    /// </summary>
    /// <remarks>
    /// Runs again whenever the viewport changes size (first sizing, rotation, a window resize), and
    /// only then: the fit itself raises ViewportChanged, which the size check turns into a no-op.
    /// </remarks>
    private void FitToRoute()
    {
        if (_route is not { } route
            || Map.Map is not { } map
            || map.Navigator.Viewport is not { Width: > 0, Height: > 0 } viewport)
        {
            return;
        }

        var size = new Size(viewport.Width, viewport.Height);

        if (size == _fittedSize)
        {
            return;
        }

        _fittedSize = size;

        var bounds = RouteGeometryLoader.Bounds(route);
        // The ETA block's edge once it is measured; a share of the panel until then.
        var left = _etaEdge > 0d ? _etaEdge + FitEtaGap : viewport.Width * FitLeftShare;
        var innerWidth = Math.Max(viewport.Width - left - FitRight, 1d);
        var innerHeight = Math.Max(viewport.Height - FitTop - FitBottom, 1d);
        var resolution = Math.Max(bounds.Width / innerWidth, bounds.Height / innerHeight);

        // The viewport centre sits off the route's centre by half the difference between opposite
        // insets. World Y points north and screen Y points down, hence the opposite signs.
        var center = new Mapsui.MPoint(
            bounds.Centroid.X - ((left - FitRight) / 2d * resolution),
            bounds.Centroid.Y + ((FitTop - FitBottom) / 2d * resolution));

        // A locked navigator ignores the fit as well, so unlock around it.
        map.Navigator.PanLock = false;
        map.Navigator.ZoomLock = false;
        map.Navigator.CenterOnAndZoomTo(center, resolution);
        map.Navigator.PanLock = true;
        map.Navigator.ZoomLock = true;
        map.Navigator.RotationLock = true;

        // The route's entrance plays once, when it is first fitted; a later resize should not replay it.
        if (!_viewportReady)
        {
            _viewportReady = true;
            Ruler.OnViewportReady();
        }
    }

    private static void DebugTrace(string message)
    {
#if DEBUG
        DebugHooks.Trace(message);
#endif
    }
}
