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
