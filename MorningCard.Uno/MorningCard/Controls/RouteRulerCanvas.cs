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

namespace MorningCard.Controls;

/// <summary>
/// The route ruler, drawn with Skia.
///
/// Two things here have no XAML or Composition equivalent: forty-odd ticks, pins and labels placed
/// from projected route geometry, and a backdrop colour filter that takes the map tiles underneath
/// close to monochrome without changing the tile source.
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

    public RouteRulerCanvas()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnActualThemeChanged;
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
        CardChrome.Changed += OnChromeChanged;

        Refresh();
        _ = ApplyTypefaceAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CardChrome.Changed -= OnChromeChanged;

        // The renderer is NOT disposed here: a re-parent fires Unloaded then Loaded, and
        // RenderOverride would then be drawing with freed native handles.
        if (Map?.Map is { } map)
        {
            map.Navigator.ViewportChanged -= OnViewportChanged;
        }
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

    /// <summary>A new trip redraws the route. There is no entrance: it is simply there.</summary>
    private void OnEtaChanged(DependencyPropertyChangedEventArgs e) => Refresh();

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        _palette = null;
        Refresh();
    }

    /// <summary>
    /// Rebuilds the snapshot on the UI thread, where theme resources and DependencyProperties are
    /// safe to read, then asks for one frame.
    /// </summary>
    private void Refresh()
    {
        // A failed or still-loading lookup has no route to draw, so only the ground is left.
        var eta = Eta is { ShowsRoute: true } value ? value : null;

        _snapshot = new RouteRulerSnapshot
        {
            HasRoute = eta is not null,
            IsLate = eta?.IsLate ?? false,
            IsLeaveNow = eta?.Tone == MorningCard.Core.Models.CardTone.LeaveNow,
            Projection = Project(eta),
            LeaveLabel = eta?.LeaveLabel ?? string.Empty,
            MapCornerRadius = MapCornerRadius(),
            // Cached: only a theme or hue change can invalidate it.
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
    }

    /// <summary>The panel radius the map's tone is clipped to, from the shared geometry tokens.</summary>
    private static float MapCornerRadius() =>
        Application.Current?.Resources.TryGetValue("MapCornerRadius", out var value) == true
            && value is CornerRadius radius
                ? (float)radius.TopLeft
                : 20f;

    /// <summary>
    /// Reads the tokens for the theme this element is actually drawn in. Application-level lookup
    /// resolves against the application's theme, which is not the same thing once the theme service
    /// has switched the root element - that mismatch left the map drawing its light palette on a
    /// dark card.
    /// </summary>
    private RouteRulerPalette ReadPalette()
    {
        // Pinned to Light, not ActualTheme. Since v2 the route is drawn over real OSM tiles, and
        // OSM has no dark style - the map stays light in both themes. Following the app theme put
        // the dark palette's near-white label ink on a light map, so "Now 7:24" and "9 min slack"
        // were white on pale grey. The ink has to follow what it is drawn on, which is the map.
        const ElementTheme theme = ElementTheme.Light;
        var fallback = RouteRulerPalette.Fallback;

        return new RouteRulerPalette(
            Map: Read("MapColor", theme, fallback.Map),
            Ink3: Read("Ink3Color", theme, fallback.Ink3),
            // Read from the brush, not the colour: the hue dial writes the brush.
            Accent: ReadBrush("AccentBrush", theme, fallback.Accent),
            OnAccent: Read("OnAccentColor", theme, fallback.OnAccent),
            Amber: Read("AmberColor", theme, fallback.Amber),
            AmberInk: Read("AmberInkColor", theme, fallback.AmberInk),
            Danger: Read("DangerColor", theme, fallback.Danger),
            DangerInk: Read("DangerInkColor", theme, fallback.DangerInk),
            Pin: Read("PinColor", theme, fallback.Pin),
            // Nodes keep a white ring in both themes: it reads as a cut-out, not as ink.
            NodeStroke: fallback.NodeStroke,
            DriveFill: Read("RulerDriveColor", theme, fallback.DriveFill),
            LeaveNowFill: Over(Read("AmberChipColor", theme, fallback.LeaveNowFill), Read("CardSurfaceColor", theme, SKColors.White)),
            LateFill: Over(Read("DangerChipColor", theme, fallback.LateFill), Read("CardSurfaceColor", theme, SKColors.White)));
    }

    /// <summary>A translucent chip colour flattened over an opaque ground.</summary>
    private static SKColor Over(SKColor top, SKColor ground)
    {
        var a = top.Alpha / 255f;
        byte Mix(byte t, byte g) => (byte)Math.Round(t * a + g * (1 - a));
        return new SKColor(Mix(top.Red, ground.Red), Mix(top.Green, ground.Green), Mix(top.Blue, ground.Blue));
    }

    private static SKColor Read(string key, ElementTheme theme, SKColor fallback) =>
        AccentPalette.Lookup(key, theme) is Windows.UI.Color color
            ? new SKColor(color.R, color.G, color.B, color.A)
            : fallback;

    private static SKColor ReadBrush(string key, ElementTheme theme, SKColor fallback) =>
        AccentPalette.Lookup(key, theme) is SolidColorBrush brush
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
