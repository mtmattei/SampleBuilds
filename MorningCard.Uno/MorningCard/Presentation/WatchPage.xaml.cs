namespace MorningCard.Presentation;

/// <summary>The Watch layout of the card: four one-face screens. Shares MainModel with the phone page; see FormFactors.</summary>
public sealed partial class WatchPage : Page
{
    private const int FaceCount = 4;

    /// <summary>The face the scroll last settled on.</summary>
    private int _face;

    public WatchPage()
    {
        InitializeComponent();
        Pages.ViewChanged += OnPagesViewChanged;
        Loaded += OnLoaded;
    }

    // WORKAROUND(Late feed content unpainted on Android Skia): on the Wear emulator the first face
    // stayed half drawn (clock, title and ring missing, headline present) until a scroll forced a
    // frame, whenever nothing else invalidated - offline, the car's clock stops ticking. Nudging the
    // scroller's opacity for the first seconds produces the frames the late content needs.
    // xaml-lint: allow codebehind - see WORKAROUND
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ticks = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            Pages.Opacity = ticks % 2 == 0 ? 0.999 : 1;
            if (++ticks >= 8)
            {
                Pages.Opacity = 1;
                timer.Stop();
            }
        };
        timer.Start();
    }

    // WORKAROUND(Snap points on a StackPanel pin the ScrollViewer on Uno Skia): VerticalSnapPointsType
    // MandatorySingle over the StackPanel snapped every swipe back to screen 1 on the Wear emulator, so
    // the scroll settles to the nearest face here instead.
    // xaml-lint: allow codebehind - see WORKAROUND
    private void OnPagesViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        var face = Pages.ViewportHeight;
        if (e.IsIntermediate || face <= 0)
        {
            return;
        }

        // One face per swipe, in the swipe's direction, once it has moved 15% of a face.
        var moved = (Pages.VerticalOffset / face) - _face;
        _face = Math.Clamp(_face + (moved > 0.15 ? 1 : moved < -0.15 ? -1 : 0), 0, FaceCount - 1);

        var target = _face * face;
        if (Math.Abs(target - Pages.VerticalOffset) > 1)
        {
            Pages.ChangeView(null, target, null, disableAnimation: false);
        }
    }
}
