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
