namespace MorningCard.Themes;

/// <summary>
/// The footer's live dot: opacity 1 to 0.35 and back over 1.8 s while the car is connected (v6).
///
/// One shared ~30 fps DispatcherTimer writing Opacity, never a Forever storyboard: on Uno Skia
/// desktop a Forever storyboard cost about a third of a core and kept costing after its target was
/// hidden, where a timer idles at 0-2% and stops itself when nothing is registered (rules file).
/// Off under reduced motion.
/// </summary>
public static class LivePulse
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(LivePulse), new PropertyMetadata(false, OnIsEnabledChanged));

    private const double PeriodSeconds = 1.8;
    private const double Floor = 0.35;

    private static readonly List<UIElement> Pulsing = [];
    private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    private static DispatcherTimer? _timer;

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnLoaded;
        element.Unloaded -= OnUnloaded;

        if (e.NewValue is true)
        {
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;

            if (element.IsLoaded)
            {
                Add(element);
            }
        }
        else
        {
            Remove(element);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Add((UIElement)sender);

    private static void OnUnloaded(object sender, RoutedEventArgs e) => Remove((UIElement)sender);

    private static void Add(UIElement element)
    {
        if (!Pulsing.Contains(element))
        {
            Pulsing.Add(element);
        }

        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += OnTick;
        }

        _timer.Start();
    }

    private static void Remove(UIElement element)
    {
        Pulsing.Remove(element);
        element.Opacity = 1;

        if (Pulsing.Count == 0)
        {
            _timer?.Stop();
        }
    }

    private static void OnTick(object? sender, object e)
    {
        var opacity = CardChrome.ReducedMotion
            ? 1d
            : Floor + ((1d - Floor) * (0.5 + (0.5 * Math.Cos(2 * Math.PI * Clock.Elapsed.TotalSeconds / PeriodSeconds))));

        foreach (var element in Pulsing)
        {
            element.Opacity = opacity;
        }
    }
}
