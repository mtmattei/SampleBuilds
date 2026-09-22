using Microsoft.UI.Xaml.Automation;
using MorningCard.Themes;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.Storage;

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
            new PropertyMetadata(null, (d, _) => ((RouteRulerCanvas)d).Refresh()));

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
            new PropertyMetadata(1d, (d, _) => ((RouteRulerCanvas)d).Refresh()));

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
        Refresh();
        _ = ApplyTypefaceAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _renderer.Dispose();

    private void OnActualThemeChanged(FrameworkElement sender, object args) => Refresh();

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
            SlackVisibleMinutes = eta?.SlackVisibleMinutes ?? 0,
            NowLabel = eta?.NowLabel ?? string.Empty,
            LeaveLabel = eta?.LeaveLabel ?? string.Empty,
            SlackLabel = eta?.SlackLabel ?? string.Empty,
            DriveLabel = eta?.DriveLabel ?? string.Empty,
            ArriveLabel = eta?.ArriveLabel ?? string.Empty,
            WalkLabel = eta?.WalkLabel ?? string.Empty,
            BellLabel = eta?.BellLabel ?? string.Empty,
            Progress = Progress,
            Palette = ReadPalette(),
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
            AccentInk: ReadBrush("AccentInkBrush", theme, fallback.AccentInk),
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
                new Uri("ms-appx:///Assets/Fonts/InstrumentSans-SemiBold.ttf"));

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
