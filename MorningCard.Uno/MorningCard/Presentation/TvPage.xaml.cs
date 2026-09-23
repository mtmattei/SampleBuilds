using MorningCard.Controls;

namespace MorningCard.Presentation;

/// <summary>The Android TV layout of the card. Shares MainModel with the phone page; see FormFactors.</summary>
public sealed partial class TvPage : Page
{
    private readonly DispatcherTimer _focusWait = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private int _focusTries;

    public TvPage()
    {
        InitializeComponent();

        _focusWait.Tick += OnFocusWaitTick;
        Loaded += (_, _) => _focusWait.Start();
        Unloaded += (_, _) => _focusWait.Stop();
    }

    /// <summary>
    /// Initial D-pad focus (spec v5 TV rule): the cabin row while warming is undecided, so OK opens
    /// Start warming; otherwise the primary action. The targets live inside FeedView templates and
    /// appear only once the feeds have values, so this waits for them (up to about five seconds).
    /// </summary>
    // xaml-lint: allow codebehind - initial D-pad focus by rule; no XAML surface sets focus from state
    private void OnFocusWaitTick(object? sender, object e)
    {
        if (++_focusTries > 20)
        {
            _focusWait.Stop();
            return;
        }

        var cabin = Find<Button>(this, "CabinRowButton");
        var primary = Find<Button>(this, "PrimaryAction");

        if (cabin is null || primary is null)
        {
            return;
        }

        var undecided = cabin.DataContext?.GetType().GetProperty("Data")?.GetValue(cabin.DataContext) is CarView { CabinPending: true };
        var target = undecided ? cabin : primary;

        if (target.Focus(FocusState.Keyboard))
        {
            _focusWait.Stop();
        }
    }

    private static T? Find<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        if (root is T match && match.Name == name)
        {
            return match;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (Find<T>(VisualTreeHelper.GetChild(root, i), name) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
