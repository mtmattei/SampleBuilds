using Microsoft.UI.Xaml.Media.Animation;
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
        WatchChecksReveal();
    }

    private long _checksToken = -1;

    /// <summary>
    /// Plays the checklist's reveal when the rows appear.
    ///
    /// The rows live inside a FeedView template, so there is no VisualState to hang this on and no
    /// element to name from outside it - watching Visibility is what is actually available. Height
    /// is deliberately not animated: measuring a growing panel every frame is the expensive way to
    /// do this, and the job here is continuity, which fade plus a short rise already carries.
    /// </summary>
    private void WatchChecksReveal()
    {
        if (FindName("ChecksBody") is not FrameworkElement body)
        {
            return;
        }

        if (_checksToken >= 0)
        {
            body.UnregisterPropertyChangedCallback(VisibilityProperty, _checksToken);
        }

        _checksToken = body.RegisterPropertyChangedCallback(
            VisibilityProperty,
            (element, _) =>
            {
                if (element is FrameworkElement { Visibility: Visibility.Visible } shown)
                {
                    PlayReveal(shown);
                }
            });
    }

    private static void PlayReveal(FrameworkElement body)
    {
        // Reduced motion resolves instantly rather than playing a shorter version of the same
        // thing: the setting means "no motion", not "less".
        if (CardChrome.ReducedMotion)
        {
            body.Opacity = 1;
            return;
        }

        var shift = body.RenderTransform as TranslateTransform;

        // Both from MotionTokens, not hand-typed here. A curve written out at each call site is how
        // two surfaces end up a hundredth apart and neither looks wrong on its own.
        var duration = TimeSpan.FromMilliseconds(
            CardMotion.Number("DurationSlowMs", 280) / Math.Max(CardChrome.MotionSpeed, 0.1));
        var ease = CardMotion.Ease("EaseSmooth");

        var board = new Storyboard();
        board.Children.Add(Fade(body, duration, ease));

        if (shift is not null)
        {
            board.Children.Add(Rise(shift, duration, ease));
        }

        board.Begin();
    }

    private static DoubleAnimationUsingKeyFrames Fade(DependencyObject target, TimeSpan duration, KeySpline ease)
    {
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
        fade.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = duration, Value = 1, KeySpline = ease });
        Storyboard.SetTarget(fade, target);
        Storyboard.SetTargetProperty(fade, "Opacity");

        return fade;
    }

    private static DoubleAnimationUsingKeyFrames Rise(TranslateTransform target, TimeSpan duration, KeySpline ease)
    {
        var rise = new DoubleAnimationUsingKeyFrames();
        rise.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 8 });
        rise.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = duration, Value = 0, KeySpline = ease });
        Storyboard.SetTarget(rise, target);
        Storyboard.SetTargetProperty(rise, "Y");

        return rise;
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

            // The fit waits for the viewport to have a size: on a slow start (the emulator) the route
            // arrived first, the navigator ignored the fit, the ruler found no viewport to choose an
            // interval for, and the route never drew. The viewport reporting a size is the signal.
            _route = geometry;
            _fittedSize = default;
            _intervalChosen = false;
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
    // a steep first drop, then an ease-out, kept from the ramp the tokens were tuned against.
    private const double ScrimFade = 72d;

    private static readonly double[] ScrimRamp = [0d, 0.16d, 0.29d, 0.45d, 0.65d, 0.84d, 1d];

    private double _etaEdge;

    /// <summary>
    /// Moves the scrim and the route's left edge to wherever the ETA text actually ends.
    /// </summary>
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

        if (EtaScrim.Background is LinearGradientBrush { GradientStops.Count: 8 } brush)
        {
            var width = edge + ScrimFade;
            var opaque = edge / width;

            EtaScrim.Width = width;

            for (var i = 0; i < ScrimRamp.Length; i++)
            {
                brush.GradientStops[i + 1].Offset = opaque + (ScrimRamp[i] * (1d - opaque));
            }
        }

        _fittedSize = default;
        FitToRoute();
    }
    private const double FitRight = 30d;
    private const double FitTop = 34d;
    private const double FitBottom = 50d;

    private RouteGeometry? _route;
    private Size _fittedSize;

    private bool _intervalChosen;

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

        // The tick interval is chosen once, for the resolution the route is first fitted at; it
        // also plays the route's entrance, which a later resize should not replay.
        if (!_intervalChosen)
        {
            _intervalChosen = true;
            Ruler.ChooseInterval();
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

    // Three 6px dots, two 4px gaps and the 5px margin before the "ready" line.
    private const double ChecksDotsWidth = 31d;

    private double _checksChipWidth;

    /// <summary>
    /// Lets the closed checklist header give way on a short row instead of clipping.
    /// </summary>
    /// <remarks>
    /// The status sits in the star column, so when the row ran out of room it was the status that
    /// vanished: on a 411 epx phone the header needed about 348 and had 297, and "3 of 3 ready"
    /// was cut off entirely. The dots go first, then the count chip, since the "ready" line says
    /// both in words. Every part but the chip measures unconstrained (Auto columns, a horizontal
    /// StackPanel), so their DesiredSize is already their natural width, margins included.
    /// </remarks>
    private void OnChecksHeaderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid row
            || row.Children.Count < 5
            || row.Children[2] is not FrameworkElement chip
            || row.Children[3] is not StackPanel { Children: [FrameworkElement dots, FrameworkElement ready] })
        {
            return;
        }

        // A collapsed chip measures to zero, so its width is remembered while it is showing.
        if (chip.Visibility == Visibility.Visible)
        {
            _checksChipWidth = chip.DesiredSize.Width;
        }

        var fixedWidth = row.Children[0].DesiredSize.Width
            + row.Children[1].DesiredSize.Width
            + ready.DesiredSize.Width
            + row.Children[4].DesiredSize.Width;

        var room = e.NewSize.Width - fixedWidth;

        chip.Visibility = room >= _checksChipWidth ? Visibility.Visible : Visibility.Collapsed;
        dots.Visibility = room >= _checksChipWidth + ChecksDotsWidth ? Visibility.Visible : Visibility.Collapsed;
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
        Glass.CornerRadius = new CornerRadius(radius);
    }
}
