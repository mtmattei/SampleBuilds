using Microsoft.UI.Xaml.Media.Animation;
using MorningCard.Core.Route;
using MorningCard.Presentation;
using MorningCard.Themes;
using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>The checklist part: accordion header and rows, with its reveal and content fit.</summary>
public sealed partial class ChecksPart : UserControl
{
    public ChecksPart()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Plays the checklist's reveal when the rows appear.
    ///
    /// The rows live inside a FeedView template, so there is no VisualState to hang this on, and
    /// FindName from outside cannot reach into the template's namescope (the card's old lookup
    /// returned null, so the reveal never played). The body announces itself through Loaded, and
    /// its Visibility is watched from there. Height is deliberately not animated: measuring a
    /// growing panel every frame is the expensive way to do this.
    /// </summary>
    // xaml-lint: allow codebehind - the reveal needs the templated body, reachable only through its own Loaded
    private void OnChecksBodyLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement body || body.Tag is "watched")
        {
            return;
        }

        body.Tag = "watched";
        body.RegisterPropertyChangedCallback(
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

    private double _checksChipWidth;

    /// <summary>
    /// Lets the closed checklist header give way on a short row instead of clipping.
    /// </summary>
    /// <remarks>
    /// On a 411 epx phone the v6 header needs about 334 epx and has about 295, so the "3 checks"
    /// chip steps aside and the "2 of 3 ready" line, which says the same in words, stays. A content
    /// fit rather than a breakpoint: it depends on the measured text, not on the window width.
    /// Children: icon, title, chip, ready line, chevron.
    /// </remarks>
    // xaml-lint: allow codebehind - content-fit on measured text widths, no Responsive equivalent
    private void OnChecksHeaderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid row
            || row.Children.Count < 5
            || row.Children[2] is not FrameworkElement chip)
        {
            return;
        }

        // A collapsed chip measures to zero, so its width is remembered while it is showing.
        // xaml-lint: allow codebehind - content-fit reads the chip state, see OnChecksHeaderSizeChanged
        if (chip.Visibility == Visibility.Visible)
        {
            _checksChipWidth = chip.DesiredSize.Width;
        }

        var fixedWidth = row.Children[0].DesiredSize.Width
            + row.Children[1].DesiredSize.Width
            + row.Children[3].DesiredSize.Width
            + row.Children[4].DesiredSize.Width
            + (row.ColumnSpacing * 5);

        // xaml-lint: allow codebehind - content-fit on measured text widths, see OnChecksHeaderSizeChanged
        chip.Visibility = e.NewSize.Width - fixedWidth >= _checksChipWidth ? Visibility.Visible : Visibility.Collapsed;
    }
}
