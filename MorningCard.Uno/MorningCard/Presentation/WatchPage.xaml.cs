using Microsoft.UI.Xaml.Input;

namespace MorningCard.Presentation;

/// <summary>The Watch layout of the card: four one-face screens. Shares MainModel with the phone page; see FormFactors.</summary>
public sealed partial class WatchPage : Page
{
    private const int FaceCount = 4;

    /// <summary>Settle glide, the house slow duration.</summary>
    private const double SettleMs = 280;

    /// <summary>A release faster than this (epx per ms) turns the page whatever the distance.</summary>
    private const double FlickSpeed = 0.3;

    /// <summary>The face the scroll last settled on.</summary>
    private int _face;

    /// <summary>The offset when the current drag started.</summary>
    private double _dragFrom;

    private bool _settling;
    private double _settleFrom;
    private double _settleTo;
    private long _settleStart;

    public WatchPage()
    {
        InitializeComponent();
        Pages.ManipulationStarted += OnPagesManipulationStarted;
        Pages.ManipulationDelta += OnPagesManipulationDelta;
        Pages.ManipulationCompleted += OnPagesManipulationCompleted;
        Pages.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPagesWheel), handledEventsToo: true);
        Loaded += OnLoaded;

        // A settle still running when the page goes would hold CompositionTarget.Rendering, which
        // makes the Skia compositor produce frames for as long as the process lives (rules file).
        Unloaded += (_, _) => StopSettle();
    }

    // WORKAROUND(Late feed content unpainted on Android Skia): on the Wear emulator the first face
    // stayed half drawn (clock, title and ring missing, headline present) until a scroll forced a
    // frame, whenever nothing else invalidated - offline, the car's clock stops ticking, and on any slow
    // start. Nudging the scroller's opacity for the first 20 seconds produces the frames the late
    // content needs.
    // xaml-lint: allow codebehind - see WORKAROUND
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ticks = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            Pages.Opacity = ticks % 2 == 0 ? 0.999 : 1;
            // 20 s: a cold-started emulator delivered the first feed values after the original 4 s.
            if (++ticks >= 40)
            {
                Pages.Opacity = 1;
                timer.Stop();
            }
        };
        timer.Start();
    }

    // WORKAROUND(Snap points on a StackPanel pin the ScrollViewer on Uno Skia): VerticalSnapPointsType
    // MandatorySingle over the StackPanel snapped every swipe back to screen 1 on the Wear emulator, so
    // the page owns the gesture. The ScrollViewer's touch scrolling is off (XAML) and it only hosts the
    // offset. The drag tracks the finger 1:1 and the snap starts on lift: letting the ScrollViewer's
    // inertia coast to rest first made every swipe two motions with a stall between them (~1.5 s).
    // xaml-lint: allow codebehind - see WORKAROUND
    private void OnPagesManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        StopSettle();
        _dragFrom = Pages.VerticalOffset;
    }

    // xaml-lint: allow codebehind - see WORKAROUND
    private void OnPagesManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        var max = Pages.ScrollableHeight;
        var offset = _dragFrom - e.Cumulative.Translation.Y;

        // Past the first or last face the page gives a third of the drag, then springs back on lift.
        if (offset < 0)
        {
            offset /= 3;
        }
        else if (offset > max)
        {
            offset = max + ((offset - max) / 3);
        }

        Pages.ChangeView(null, offset, null, disableAnimation: true);
    }

    // xaml-lint: allow codebehind - see WORKAROUND
    private void OnPagesManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        var face = Pages.ViewportHeight;
        if (face <= 0)
        {
            return;
        }

        // One face per swipe: a flick faster than FlickSpeed, or a drag past 15% of a face.
        var moved = -e.Cumulative.Translation.Y / face;
        var speed = -e.Velocities.Linear.Y;
        var step = speed > FlickSpeed || moved > 0.15 ? 1 : speed < -FlickSpeed || moved < -0.15 ? -1 : 0;
        GoToFace(_face + step);
    }

    // The rotary crown and a mouse wheel arrive as wheel events: one face per notch.
    // xaml-lint: allow codebehind - see WORKAROUND
    private void OnPagesWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(Pages).Properties.MouseWheelDelta;
        if (delta != 0)
        {
            GoToFace(_face + (delta < 0 ? 1 : -1));
            e.Handled = true;
        }
    }

    private void GoToFace(int face)
    {
        _face = Math.Clamp(face, 0, FaceCount - 1);
        StartSettle(_face * Pages.ViewportHeight);
    }

    // One-shot frame loop for the settle: unsubscribed as soon as it lands, so it never pumps idle frames.
    private void StartSettle(double target)
    {
        StopSettle();
        _settleFrom = Pages.VerticalOffset;
        _settleTo = target;
        _settleStart = Environment.TickCount64;
        if (Math.Abs(_settleTo - _settleFrom) <= 1)
        {
            return;
        }

        _settling = true;
        CompositionTarget.Rendering += OnSettleFrame;
    }

    private void StopSettle()
    {
        if (_settling)
        {
            _settling = false;
            CompositionTarget.Rendering -= OnSettleFrame;
        }
    }

    // xaml-lint: allow codebehind - see WORKAROUND (snap points); a one-shot frame loop for the settle
    private void OnSettleFrame(object? sender, object e)
    {
        var p = Math.Min(1, (Environment.TickCount64 - _settleStart) / SettleMs);
        var offset = _settleFrom + ((_settleTo - _settleFrom) * EaseSmooth(p));
        Pages.ChangeView(null, offset, null, disableAnimation: true);
        if (p >= 1)
        {
            StopSettle();
        }
    }

    /// <summary>The house EaseSmooth curve, cubic-bezier(0.22, 1, 0.36, 1), solved for x by bisection.</summary>
    private static double EaseSmooth(double x)
    {
        const double x1 = 0.22, x2 = 0.36;
        double lo = 0, hi = 1, t = x;
        for (var i = 0; i < 20; i++)
        {
            t = (lo + hi) / 2;
            var bx = (3 * (1 - t) * (1 - t) * t * x1) + (3 * (1 - t) * t * t * x2) + (t * t * t);
            if (bx < x)
            {
                lo = t;
            }
            else
            {
                hi = t;
            }
        }

        // y1 = y2 = 1, so y(t) = 3(1-t)^2 t + 3(1-t) t^2 + t^3.
        return (3 * (1 - t) * (1 - t) * t) + (3 * (1 - t) * t * t) + (t * t * t);
    }
}
