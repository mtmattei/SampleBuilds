using Microsoft.UI.Xaml.Input;
using MorningCard.Controls;
using Windows.System;

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
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>
    /// A D-pad press with nothing on the page focused puts focus back on the start target instead
    /// of being lost. Measured on the TV emulator: focus went nowhere when the feeds arrived after
    /// the initial wait, and when collapsing the checklist rebuilt the focused header, and from
    /// then on the remote did nothing.
    /// </summary>
    // xaml-lint: allow codebehind - D-pad focus recovery; no XAML surface restores lost focus
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not (VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right
            or VirtualKey.GamepadDPadUp or VirtualKey.GamepadDPadDown or VirtualKey.GamepadDPadLeft
            or VirtualKey.GamepadDPadRight or VirtualKey.Enter or VirtualKey.GamepadA))
        {
            return;
        }

        if (XamlRoot is { } root && FocusManager.GetFocusedElement(root) is DependencyObject focused && IsInside(focused))
        {
            return;
        }

        e.Handled = FocusStart();
    }

    private bool IsInside(DependencyObject element)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, this))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Initial D-pad focus (spec v5 TV rule): the cabin row while warming is undecided, so OK opens
    /// Start warming; otherwise the primary action. The targets live inside FeedView templates and
    /// appear only once the feeds have values, so this waits for them (up to about 20 seconds).
    /// </summary>
    // xaml-lint: allow codebehind - initial D-pad focus by rule; no XAML surface sets focus from state
    private void OnFocusWaitTick(object? sender, object e)
    {
        // 20 s: a cold emulator delivered the feeds after the original 5 s. A later D-pad press
        // recovers focus anyway (OnPreviewKeyDown).
        if (++_focusTries > 80 || FocusStart())
        {
            _focusWait.Stop();
        }
    }

    private bool FocusStart()
    {
        var cabin = Find<Button>(this, "CabinRowButton");
        var primary = Find<Button>(this, "PrimaryAction");

        if (cabin is null || primary is null)
        {
            return false;
        }

        var undecided = cabin.DataContext?.GetType().GetProperty("Data")?.GetValue(cabin.DataContext) is CarView { CabinPending: true };
        return (undecided ? cabin : primary).Focus(FocusState.Keyboard);
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
