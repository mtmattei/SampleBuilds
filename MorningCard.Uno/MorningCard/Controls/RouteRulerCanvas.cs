// Uno ships its own MapControl in Microsoft.UI.Xaml.Controls, which has no desktop support and is
// not the one here. Aliased rather than fully qualified, which would read the same eight times.
using MapControl = Mapsui.UI.WinUI.MapControl;
using Microsoft.UI.Xaml.Automation;
using MorningCard.Core.Route;
using MorningCard.Themes;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace MorningCard.Controls;

/// <summary>
/// The route ruler, drawn with Skia.
///
/// Three things here have no XAML or Composition equivalent: positions along a path (every node,
/// dot, pill and label is placed by distance, not by coordinates), label text with a halo, and one
/// timeline driving forty elements. Partial path segments alone would not need Skia -
/// CompositionGeometry.TrimStart is implemented on this renderer - but the positions would still
/// have to come from SKPathMeasure, which would split one drawing across two systems.
///
/// The cost is that the labels are invisible to the automation tree, so the element carries one
/// composed description of the whole route instead.
/// </summary>
public sealed partial class RouteRulerCanvas : SKCanvasElement
{
    private static Task<SKTypeface?>? _typefaceLoad;

    private readonly RouteRulerRenderer _renderer = new();
    private RouteRulerSnapshot _snapshot = new();
    private RouteRulerPalette? _palette;

    /// <summary>
    /// How long the whole entrance runs at speed 1: dots, then the drive drawing along the path,
    /// then the walk, then the markers popping in, then the labels.
    /// </summary>
    private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(1700);

    /// <summary>
    /// 30fps. A DispatcherTimer produces exactly one frame per invalidation, where a held
    /// CompositionTarget.Rendering subscription would pin the compositor to display refresh for the
    /// life of the app - measured at 50 to 70 percent of a core, whatever is on screen.
    /// </summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    private readonly DispatcherTimer _entrance = new() { Interval = FrameInterval };
    private DateTimeOffset _entranceStarted;

    public RouteRulerCanvas()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _entrance.Tick += OnEntranceTick;
    }

    public static readonly DependencyProperty EtaProperty =
        DependencyProperty.Register(
            nameof(Eta),
            typeof(EtaView),
            typeof(RouteRulerCanvas),
            new PropertyMetadata(null, (d, e) => ((RouteRulerCanvas)d).OnEtaChanged(e)));

    /// <summary>The trip to draw. Null means no trip today, which leaves only the map ground.</summary>
    public EtaView? Eta
    {
        get => (EtaView?)GetValue(EtaProperty);
        set => SetValue(EtaProperty, value);
    }

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(RouteRulerCanvas),
            // Progress alone does not rebuild the snapshot. A full Refresh performs 15 resource
            // lookups, each walking the whole merged dictionary tree, and this fires 30 times a
            // second through the entrance - about 450 tree walks per second on the UI thread.
            new PropertyMetadata(1d, (d, _) => ((RouteRulerCanvas)d).AdvanceProgress()));

    /// <summary>0 to 1 across the entrance. The motion step drives this; 1 is settled.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public static readonly DependencyProperty GeometryProperty =
        DependencyProperty.Register(
            nameof(Geometry),
            typeof(RouteGeometry),
            typeof(RouteRulerCanvas),
            new PropertyMetadata(null, (d, _) => ((RouteRulerCanvas)d).Refresh()));

    /// <summary>
    /// The committed route. Null until the asset has loaded, which leaves the tiles on their own -
    /// the same as a morning with no trip.
    /// </summary>
    public RouteGeometry? Geometry
    {
        get => (RouteGeometry?)GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public static readonly DependencyProperty MapProperty =
        DependencyProperty.Register(
            nameof(Map),
            typeof(MapControl),
            typeof(RouteRulerCanvas),
            new PropertyMetadata(null, (d, e) => ((RouteRulerCanvas)d).OnMapChanged(e)));

    /// <summary>
    /// The map this canvas draws over. Its viewport is the only thing that turns world coordinates
    /// into the pixels underneath, so the route cannot be projected without it.
    /// </summary>
    public MapControl? Map
    {
        get => (MapControl?)GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    /// <summary>
    /// The tick interval, chosen once from the resolution the map was fitted at. The map is pan-
    /// and zoom-locked, so that is the only resolution the route is ever drawn at.
    /// </summary>
    private int _interval = 1;

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        // Render thread: read the snapshot, draw, allocate nothing.
        _renderer.Render(canvas, new SKSize((float)area.Width, (float)area.Height), _snapshot);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CardMotion.ReplayRequested += OnReplayRequested;
        CardChrome.Changed += OnChromeChanged;

        Refresh();
        _ = ApplyTypefaceAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CardMotion.ReplayRequested -= OnReplayRequested;
        CardChrome.Changed -= OnChromeChanged;

        if (Map?.Map is { } map)
        {
            map.Navigator.ViewportChanged -= OnViewportChanged;
        }

        // A timer left running would keep asking for frames after the card is gone. The renderer
        // is NOT disposed here: a re-parent fires Unloaded then Loaded, and RenderOverride would
        // then be drawing with freed native handles.
        _entrance.Stop();
    }

    private void OnChromeChanged(object? sender, EventArgs e)
    {
        _palette = null;
        Refresh();
    }

    /// <summary>
    /// Follows the map's viewport, because every projected point moves with it.
    /// </summary>
    /// <remarks>
    /// With the map pan- and zoom-locked this fires on the initial fit and on resize, not on every
    /// frame of a drag - so reprojecting the whole route here is cheap. It arrives off the UI
    /// thread, hence the hop.
    /// </remarks>
    private void OnMapChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MapControl old && old.Map is not null)
        {
            old.Map.Navigator.ViewportChanged -= OnViewportChanged;
        }

        if (e.NewValue is MapControl map && map.Map is not null)
        {
            map.Map.Navigator.ViewportChanged += OnViewportChanged;
        }

        Refresh();
    }

    private void OnViewportChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(Refresh);

    /// <summary>Cheap path for the entrance: one field write and one frame, no resource lookups.</summary>
    private void AdvanceProgress()
    {
        _snapshot = _snapshot with { Progress = Progress };
        Invalidate();
    }

    private void OnReplayRequested(object? sender, EventArgs e) => PlayEntrance();

    /// <summary>
    /// The entrance plays when a route first arrives, and again on an explicit replay. It does not
    /// replay for every clock tick or dial move, which would make the card twitch.
    /// </summary>
    private void OnEtaChanged(DependencyPropertyChangedEventArgs e)
    {
        var hadRoute = e.OldValue is EtaView { Failed: false };
        var hasRoute = e.NewValue is EtaView { Failed: false };

        // Refresh first, always. PlayEntrance only moves Progress, and since a Progress change no
        // longer rebuilds the snapshot, returning straight into it left the route undrawn - the ETA
        // text was present while the map showed bare ground.
        Refresh();

        if (hasRoute && !hadRoute)
        {
            PlayEntrance();
        }
    }

    private void PlayEntrance()
    {
        if (ReducedMotion)
        {
            _entrance.Stop();
            Progress = 1d;
            Refresh();
            return;
        }

        _entranceStarted = DateTimeOffset.UtcNow;
        Progress = 0d;
        _entrance.Start();
    }

    private void OnEntranceTick(object? sender, object e)
    {
        var scaled = EntranceDuration / Math.Max(0.25d, CardChrome.MotionSpeed);
        var elapsed = DateTimeOffset.UtcNow - _entranceStarted;
        var progress = Math.Clamp(elapsed / scaled, 0d, 1d);

        Progress = progress;

        if (progress >= 1d)
        {
            // One-shot: the timer stops rather than idling at 30fps for the rest of the session.
            _entrance.Stop();
        }
    }

    /// <summary>
    /// On desktop <c>UISettings.AnimationsEnabled</c> is hardcoded true, so the Simulator's switch
    /// is the only signal there. On Android the two are combined.
    /// </summary>
    private static bool ReducedMotion
    {
        get
        {
            if (CardChrome.ReducedMotion)
            {
                return true;
            }

            try
            {
                return !new UISettings().AnimationsEnabled;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Rebuilds the snapshot on the UI thread, where theme resources and DependencyProperties are
    /// safe to read, then asks for one frame.
    /// </summary>
    private void Refresh()
    {
        // A failed lookup has no route to draw, so only the ground is left, same as no trip at all.
        var eta = Eta is { Failed: false } value ? value : null;

        _snapshot = new RouteRulerSnapshot
        {
            HasRoute = eta is not null,
            IsLate = eta?.IsLate ?? false,
            Projection = Project(eta),
            LeaveLabel = eta?.LeaveLabel ?? string.Empty,
            ArriveLabel = eta?.ArriveLabel ?? string.Empty,
            BellLabel = eta?.BellLabel ?? string.Empty,
            Progress = Progress,
            // Cached: the tokens are fixed for the life of the app.
            Palette = _palette ??= ReadPalette(),
        };

        AutomationProperties.SetName(this, eta?.RouteDescription ?? string.Empty);

        Invalidate();
    }

    /// <summary>
    /// Lays the morning's times onto the route and projects the result into this canvas's pixels.
    /// </summary>
    /// <remarks>
    /// Returns null until all three parts are present - the trip, the asset and a sized map. Any
    /// one of them missing leaves the tiles showing on their own, which is honest: there is nothing
    /// to say about a route the card cannot place.
    /// </remarks>
    private RulerProjection? Project(EtaView? eta)
    {
        if (eta?.Ruler is not { } plan || Geometry is not { } geometry)
        {
            return null;
        }

        if (Map?.Map?.Navigator.Viewport is not { Width: > 0, Height: > 0 } viewport)
        {
            return null;
        }

        var layout = TimeRulerLayout.Compute(
            geometry, plan.Timeline, plan.WalkMin, plan.Bell, plan.Now, _interval);

        return RulerProjection.Create(layout, viewport);
    }

    /// <summary>
    /// Picks the tick interval for the resolution the map has settled at. Called once per viewport,
    /// not per refresh: the answer only changes when the map is resized.
    /// </summary>
    public void ChooseInterval()
    {
        if (Geometry is not { } geometry ||
            Map?.Map?.Navigator.Viewport is not { Width: > 0 } viewport)
        {
            return;
        }

        _interval = TimeRulerLayout.ChooseInterval(geometry, viewport.Resolution);
        Refresh();

        // The asset loads after the trip does, so the entrance that played when the ETA arrived had
        // nothing to draw. This is the first moment there is a route on screen, so it plays here.
        if (_snapshot.Projection is not null)
        {
            PlayEntrance();
        }
    }

    /// <summary>
    /// Reads the card tokens once. The map is light, and so is the app (v6 decision B6).
    /// </summary>
    private static RouteRulerPalette ReadPalette()
    {
        var fallback = RouteRulerPalette.Fallback;

        return new RouteRulerPalette(
            Map: Read("MapColor", fallback.Map),
            Road: Read("MapRoadColor", fallback.Road),
            Park: Read("MapParkColor", fallback.Park),
            Water: Read("MapWaterColor", fallback.Water),
            Ink: Read("InkColor", fallback.Ink),
            Ink2: Read("Ink2Color", fallback.Ink2),
            Ink3: Read("Ink3Color", fallback.Ink3),
            Accent: ReadBrush("AccentBrush", fallback.Accent),
            OnAccent: Read("OnAccentColor", fallback.OnAccent),
            Amber: Read("AmberColor", fallback.Amber),
            AmberInk: Read("AmberInkColor", fallback.AmberInk),
            Danger: Read("DangerColor", fallback.Danger),
            DangerInk: Read("DangerInkColor", fallback.DangerInk),
            Pin: Read("PinColor", fallback.Pin),
            // Nodes keep a white ring: it reads as a cut-out, not as ink.
            NodeStroke: fallback.NodeStroke);
    }

    private static SKColor Read(string key, SKColor fallback) =>
        CardPalette.Lookup(key) is Windows.UI.Color color
            ? new SKColor(color.R, color.G, color.B, color.A)
            : fallback;

    private static SKColor ReadBrush(string key, SKColor fallback) =>
        CardPalette.Lookup(key) is SolidColorBrush brush
            ? new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A)
            : fallback;

    private async Task ApplyTypefaceAsync()
    {
        var typeface = await LoadTypefaceAsync();

        if (typeface is not null)
        {
            _renderer.SetTypeface(typeface);
            Invalidate();
        }
    }

    /// <summary>
    /// Loads the packaged face once for every canvas. Skia cannot read a XAML FontFamily, so the
    /// same TTF the card's text styles use is opened as a stream here.
    /// </summary>
    private static Task<SKTypeface?> LoadTypefaceAsync() =>
        _typefaceLoad ??= LoadCoreAsync();

    private static async Task<SKTypeface?> LoadCoreAsync()
    {
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///Assets/Fonts/InstrumentSans_SemiBold.ttf"));

            using var stream = await file.OpenStreamForReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;

            return SKTypeface.FromStream(memory);
        }
        catch (Exception error)
        {
            // The default face is a legible fallback, so this must not throw - but it must not be
            // silent either. A swallowed failure here is exactly how the Android hyphen-to-
            // underscore asset rename went unnoticed: the labels just quietly changed font.
            DebugTrace($"typeface load failed: {error.GetType().Name}: {error.Message}");
            return null;
        }
    }
    /// <summary>
    /// Reports an asset failure without taking the card down. Writes to MC_TRACE in DEBUG, and to
    /// the debugger output otherwise, so a silent fallback still leaves a trail.
    /// </summary>
    private static void DebugTrace(string message)
    {
        System.Diagnostics.Debug.WriteLine($"RouteRulerCanvas: {message}");

        var path = Environment.GetEnvironmentVariable("MC_TRACE");

        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                File.AppendAllText(path, $"RouteRulerCanvas: {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // A trace that cannot be written must not take the app with it.
            }
        }
    }
}
