using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Carte.Controls;

/// <summary>Layered native vector artwork. Only internal parts animate; layout stays fixed.</summary>
public sealed class LivingIllustrationView : UserControl
{
    private readonly UISettings _settings = new();
    private readonly DispatcherTimer _motionGuard = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private FrameworkElement? _drawing;
    private FrameworkElement? _inputOwner;
    private Storyboard? _storyboard;
    private IllustrationDefinition? _definition;
    private bool _hovering;
    private bool _hasVisibleViewport;
    private bool _appearancePlayed;
    private readonly DispatcherTimer _appearanceTimer = new();


    public LivingIllustrationView()
    {
        Width = Height = 38;
        IsTabStop = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        EffectiveViewportChanged += OnEffectiveViewportChanged;
        _appearanceTimer.Tick += (_, _) =>
        {
            _appearanceTimer.Stop();
            if (!CanPlayAppearance()) return;
            _appearancePlayed = true;
#if DEBUG
            Console.WriteLine($"Carte appearance: {IllustrationKey}, visit {AppearanceId}.");
#endif
            Play();
        };
        // Uno 6.7 does not implement AnimationsEnabledChanged. Poll only while a
        // short sequence is active; never keep an idle/background polling loop.
        _motionGuard.Tick += (_, _) => { if (!_settings.AnimationsEnabled || !MotionEnabled) Stop(); };
        SetPalette();
    }

    public static readonly DependencyProperty IllustrationKeyProperty = DependencyProperty.Register(
        nameof(IllustrationKey), typeof(string), typeof(LivingIllustrationView), new PropertyMetadata("", (d, _) => ((LivingIllustrationView)d).Rebuild()));
    public static readonly DependencyProperty FallbackSourceProperty = DependencyProperty.Register(
        nameof(FallbackSource), typeof(ImageSource), typeof(LivingIllustrationView), new PropertyMetadata(null, (d, _) => ((LivingIllustrationView)d).UpdateFallback()));
    public static readonly DependencyProperty FallbackInvertedSourceProperty = DependencyProperty.Register(
        nameof(FallbackInvertedSource), typeof(ImageSource), typeof(LivingIllustrationView), new PropertyMetadata(null, (d, _) => ((LivingIllustrationView)d).UpdateFallback()));
    public static readonly DependencyProperty IsInvertedProperty = DependencyProperty.Register(
        nameof(IsInverted), typeof(bool), typeof(LivingIllustrationView), new PropertyMetadata(false, (d, _) => ((LivingIllustrationView)d).SetPalette()));
    public static readonly DependencyProperty SurfaceBrushProperty = DependencyProperty.Register(
        nameof(SurfaceBrush), typeof(Brush), typeof(LivingIllustrationView), new PropertyMetadata(null));
    public static readonly DependencyProperty MotionEnabledProperty = DependencyProperty.Register(
        nameof(MotionEnabled), typeof(bool), typeof(LivingIllustrationView), new PropertyMetadata(true, (d, e) => { if (!(bool)e.NewValue) ((LivingIllustrationView)d).Stop(); }));

    public static readonly DependencyProperty AppearanceIdProperty = DependencyProperty.Register(
        nameof(AppearanceId), typeof(int), typeof(LivingIllustrationView), new PropertyMetadata(0, (d, _) => ((LivingIllustrationView)d).ResetAppearance()));
    public static readonly DependencyProperty AppearanceDelayMillisecondsProperty = DependencyProperty.Register(
        nameof(AppearanceDelayMilliseconds), typeof(int), typeof(LivingIllustrationView), new PropertyMetadata(350));

    // A new visit token re-arms the intro; selection and normal layout changes do not.
    public int AppearanceId { get => (int)GetValue(AppearanceIdProperty); set => SetValue(AppearanceIdProperty, value); }
    public int AppearanceDelayMilliseconds { get => (int)GetValue(AppearanceDelayMillisecondsProperty); set => SetValue(AppearanceDelayMillisecondsProperty, value); }

    public string IllustrationKey { get => (string)GetValue(IllustrationKeyProperty); set => SetValue(IllustrationKeyProperty, value); }
    public ImageSource? FallbackSource { get => (ImageSource?)GetValue(FallbackSourceProperty); set => SetValue(FallbackSourceProperty, value); }
    public ImageSource? FallbackInvertedSource { get => (ImageSource?)GetValue(FallbackInvertedSourceProperty); set => SetValue(FallbackInvertedSourceProperty, value); }
    public bool IsInverted { get => (bool)GetValue(IsInvertedProperty); set => SetValue(IsInvertedProperty, value); }
    public Brush SurfaceBrush { get => (Brush)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public bool MotionEnabled { get => (bool)GetValue(MotionEnabledProperty); set => SetValue(MotionEnabledProperty, value); }

    private void SetPalette()
    {
        Foreground = (Brush)Application.Current.Resources[IsInverted ? "CartePaperBrush" : "CarteInkBrush"];
        SurfaceBrush = (Brush)Application.Current.Resources[IsInverted ? "CarteInkBrush" : "CartePaperBrush"];
        UpdateFallback();
    }

    private void Rebuild()
    {
        Stop();
        _drawing = null;
        _definition = null;
        if (IllustrationCatalog.Items.TryGetValue(IllustrationKey ?? "", out var definition))
        {
            _definition = definition;
            _drawing = (FrameworkElement)XamlReader.Load(definition.Xaml);
            _drawing.DataContext = this;
            Content = new Viewbox { Stretch = Stretch.Uniform, Child = _drawing, IsHitTestVisible = false };
        }
        else UpdateFallback();
        ResetAppearance();
    }

    private void UpdateFallback()
    {
        if (_definition is null) Content = new Image { Source = IsInverted ? FallbackInvertedSource ?? FallbackSource : FallbackSource, Stretch = Stretch.Uniform, IsHitTestVisible = false };
    }

    public void Play()
    {
        Stop();
        _appearancePlayed = true;
        if (!IsLoaded || !MotionEnabled || !_settings.AnimationsEnabled || _drawing is null || _definition is null) return;
#if DEBUG
        if (Environment.GetEnvironmentVariable("CARTE_REDUCED_MOTION") == "1") return;
#endif
        var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };
        foreach (var track in _definition.Tracks)
        {
            if (_drawing.FindName(track.Target) is not UIElement layer) throw new InvalidOperationException($"Missing illustration layer {track.Target}");
            // XamlReader registers named UIElements, but not nested Transform objects.
            DependencyObject target = track.Property == "Opacity" ? layer : layer.RenderTransform;
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(track.Duration)),
                BeginTime = TimeSpan.FromMilliseconds(track.Delay),
                EnableDependentAnimation = true
            };
            foreach (var frame in track.Frames)
            {
                animation.KeyFrames.Add(new SplineDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(track.Duration * frame.Offset)),
                    Value = frame.Value,
                    KeySpline = new KeySpline { ControlPoint1 = new Point(.4, 0), ControlPoint2 = new Point(.2, 1) }
                });
            }
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, track.Property);
            storyboard.Children.Add(animation);
        }
        _storyboard = storyboard;
        storyboard.Completed += (_, _) => { if (ReferenceEquals(_storyboard, storyboard)) Stop(); };
        storyboard.Begin();
        _motionGuard.Start();
#if DEBUG
        Console.WriteLine($"Carte illustration: {IllustrationKey}, {storyboard.Children.Count} native part tracks.");
#endif
    }

    public void Stop()
    {
        var previous = _storyboard;
        _storyboard = null;
        previous?.Stop();
        _motionGuard.Stop();
        _appearanceTimer.Stop();
    }

    private void ResetAppearance()
    {
        Stop();
        _appearancePlayed = false;
        QueueAppearance();
    }

    private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs e)
    {
        var viewport = e.EffectiveViewport;
        _hasVisibleViewport = viewport.Width > 0 && viewport.Height > 0
            && viewport.Right > 0 && viewport.Bottom > 0
            && viewport.Left < ActualWidth && viewport.Top < ActualHeight;
        if (_hasVisibleViewport) QueueAppearance();
        else _appearanceTimer.Stop();
    }

    private bool CanPlayAppearance()
    {
        if (!IsLoaded || AppearanceId <= 0 || _appearancePlayed || !_hasVisibleViewport || _definition is null)
            return false;
        for (DependencyObject? parent = this; parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is UIElement { Visibility: Visibility.Collapsed }) return false;
        return true;
    }

    private void QueueAppearance()
    {
        if (!CanPlayAppearance() || _appearanceTimer.IsEnabled) return;
        _appearanceTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(AppearanceDelayMilliseconds, 0, 1500));
        _appearanceTimer.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DetachInput();
        for (DependencyObject? parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is Button or ListViewItem)
            {
                _inputOwner = (FrameworkElement)parent;
                break;
            }
        }
        if (_inputOwner is not null)
        {
            _inputOwner.AddHandler(PointerEnteredEvent, new PointerEventHandler(OnPointerEntered), true);
            _inputOwner.AddHandler(PointerExitedEvent, new PointerEventHandler(OnPointerExited), true);
            _inputOwner.AddHandler(TappedEvent, new TappedEventHandler(OnTapped), true);
            _inputOwner.AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        }
        QueueAppearance();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Stop();
        _hasVisibleViewport = false;
        DetachInput();
    }

    private void DetachInput()
    {
        if (_inputOwner is null) return;
        _inputOwner.RemoveHandler(PointerEnteredEvent, new PointerEventHandler(OnPointerEntered));
        _inputOwner.RemoveHandler(PointerExitedEvent, new PointerEventHandler(OnPointerExited));
        _inputOwner.RemoveHandler(TappedEvent, new TappedEventHandler(OnTapped));
        _inputOwner.RemoveHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown));
        _inputOwner = null;
        _hovering = false;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_hovering && e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse && IsInsideOwner(e))
        {
            _hovering = true;
            Play();
        }
    }

    private bool IsInsideOwner(PointerRoutedEventArgs e)
    {
        if (_inputOwner is null) return false;
        var p = e.GetCurrentPoint(_inputOwner).Position;
        return p.X >= 0 && p.Y >= 0 && p.X < _inputOwner.ActualWidth && p.Y < _inputOwner.ActualHeight;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) { if (!IsInsideOwner(e)) _hovering = false; }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        // Nested Save / Why / Remove controls own their own interaction.
        for (var source = e.OriginalSource as DependencyObject; source is not null && source != _inputOwner; source = VisualTreeHelper.GetParent(source))
            if (source is Button) return;
        Play();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e) => Stop();
}
