using Microsoft.UI.Xaml.Automation;
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
        ActualThemeChanged += OnActualThemeChanged;
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
        // A failed lookup has no route to draw, so only the ground is left, same as no trip at all.
        var eta = Eta is { Failed: false } value ? value : null;

        _snapshot = new RouteRulerSnapshot
        {
            HasRoute = eta is not null,
            IsLate = eta?.IsLate ?? false,
            Leave = eta?.RouteLeave ?? 0d,
            Arrive = eta?.RouteArrive ?? 1d,
            NowLabel = eta?.NowLabel ?? string.Empty,
            LeaveLabel = eta?.LeaveLabel ?? string.Empty,
            SlackLabel = eta?.SlackLabel ?? string.Empty,
            DriveLabel = eta?.DriveLabel ?? string.Empty,
            ArriveLabel = eta?.ArriveLabel ?? string.Empty,
            WalkLabel = eta?.WalkLabel ?? string.Empty,
            BellLabel = eta?.BellLabel ?? string.Empty,
            Progress = Progress,
            // Cached: only a theme or hue change can invalidate it.
            Palette = _palette ??= ReadPalette(),
        };

        AutomationProperties.SetName(this, eta?.RouteDescription ?? string.Empty);

        Invalidate();
    }

    /// <summary>
    /// Reads the tokens for the theme this element is actually drawn in. Application-level lookup
    /// resolves against the application's theme, which is not the same thing once the theme service
    /// has switched the root element - that mismatch left the map drawing its light palette on a
    /// dark card.
    /// </summary>
    private RouteRulerPalette ReadPalette()
    {
        var theme = ActualTheme;
        var fallback = RouteRulerPalette.Fallback;

        return new RouteRulerPalette(
            Map: Read("MapColor", theme, fallback.Map),
            Road: Read("MapRoadColor", theme, fallback.Road),
            Park: Read("MapParkColor", theme, fallback.Park),
            Water: Read("MapWaterColor", theme, fallback.Water),
            Ink: Read("InkColor", theme, fallback.Ink),
            Ink2: Read("Ink2Color", theme, fallback.Ink2),
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
            NodeStroke: fallback.NodeStroke);
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
        catch (Exception)
        {
            // The default face is a legible fallback; losing the map labels would not be.
            return null;
        }
    }
}





