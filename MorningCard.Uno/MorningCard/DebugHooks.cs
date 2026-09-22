#if DEBUG
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Services;

namespace MorningCard;

/// <summary>
/// Environment hooks so a verification run can pin the app's start state without a pointer.
/// DEBUG only, and inert unless a variable is set.
///
/// <list type="bullet">
/// <item><c>MC_STATE</c>: live, loading, offline, empty, error.</item>
/// <item><c>MC_CLOCK</c>: minutes since midnight, e.g. 460 for 7:40.</item>
/// <item><c>MC_DELAY</c>: traffic delay in minutes.</item>
/// <item><c>MC_RANGE</c>, <c>MC_TIRE</c>: stat values, for the warning thresholds.</item>
/// <item><c>MC_THEME</c>: light or dark. Applied after navigation, so it beats the saved choice.</item>
/// <item><c>MC_PROBE_RETRY</c>: invokes the error template's Retry command and traces the result.</item>
/// <item><c>MC_TRACE</c>: file to append traces to. A desktop app is windowed, so redirected
/// stdout captures nothing.</item>
/// </list>
/// </summary>
internal static class DebugHooks
{
    public static void ApplyTo(SimulatedDriveHub hub)
    {
        if (Read("MC_CLOCK") is { } clock && int.TryParse(clock, out var minutes))
        {
            hub.SetClock(minutes);
        }

        if (Read("MC_DELAY") is { } delay && int.TryParse(delay, out var delayMin))
        {
            hub.SetTrafficDelay(delayMin);
        }

        if (Read("MC_RANGE") is { } range && int.TryParse(range, out var rangeMi))
        {
            hub.SetRange(rangeMi);
        }

        if (Read("MC_TIRE") is { } tire && int.TryParse(tire, out var psi))
        {
            hub.SetTireFrontLeft(psi);
        }
        if (Read("MC_REDUCEDMOTION") == "1")
        {
            Themes.CardChrome.ReducedMotion = true;
        }

        if (Read("MC_MOTION") is { } motion && double.TryParse(motion, CultureInfo.InvariantCulture, out var speed))
        {
            Themes.CardChrome.MotionSpeed = speed;
        }


        switch (Read("MC_STATE")?.ToLowerInvariant())
        {
            case "loading":
                hub.SetMode(ConnectionMode.Loading);
                break;
            case "offline":
                hub.SetMode(ConnectionMode.Offline);
                break;
            case "empty":
            case "notrip":
                hub.SetMode(ConnectionMode.NoTrip);
                break;
            case "error":
                hub.InjectFault();
                break;
        }
    }

    public static ElementTheme? Theme => Read("MC_THEME")?.ToLowerInvariant() switch
    {
        "dark" => ElementTheme.Dark,
        "light" => ElementTheme.Light,
        _ => null,
    };

    public static bool ProbeRetry => Read("MC_PROBE_RETRY") == "1";

    public static bool ProbeFocus => Read("MC_PROBE_FOCUS") == "1";

    /// <summary>
    /// Walks the tab order and traces it.
    ///
    /// Synthesised keystrokes are unreliable against an Uno desktop window from a background
    /// process, so this asks the focus manager the same question the Tab key would:
    /// FindNextElement in the Next direction, repeatedly, from the first focusable element. It
    /// reports every stop, and flags anything focusable that cannot be activated.
    /// </summary>
    public static async Task ProbeFocusAsync(DependencyObject root)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));

        var options = new FindNextElementOptions { SearchRoot = root };

        // FindNextElement only ever returns the two top-level FeedViews here, whatever holds focus,
        // so it does not walk into templated children on this Uno version and cannot answer the
        // question. Enumerating the tree's tab stops does: it says directly whether every action is
        // reachable, and flags any stop that leads nowhere.
        var stops = new List<string>();
        CollectTabStops(root, stops);
        Trace($"PROBE focus: {stops.Count} tab stops -> {string.Join(" | ", stops)}");

        var seen = new List<string>();
        var current = FocusManager.FindFirstFocusableElement(root) as DependencyObject;

        for (var step = 0; step < 40 && current is not null; step++)
        {
            var label = Describe(current);

            if (seen.Contains(label))
            {
                Trace($"PROBE focus: order wrapped after {seen.Count} stops");
                break;
            }

            seen.Add(label);

            // Focus has to move before asking for the next element: FindNextElement searches from
            // whatever currently holds focus, so calling it first just returns the same element and
            // the walk reports a two-stop loop that is not real.
            if (current is Control control)
            {
                control.Focus(FocusState.Keyboard);
            }
            else if (current is UIElement element)
            {
                element.Focus(FocusState.Keyboard);
            }

            current = FocusManager.FindNextElement(FocusNavigationDirection.Next, options) as DependencyObject;
        }

        Trace($"PROBE focus: {seen.Count} stops -> {string.Join(" | ", seen)}");
    }

    private static void CollectTabStops(DependencyObject element, List<string> stops)
    {
        if (element is Control { IsTabStop: true } control)
        {
            var reachable = control.IsEnabled ? string.Empty : " DISABLED-so-skipped";
            stops.Add($"{Describe(control)}{reachable}");
        }

        var count = VisualTreeHelper.GetChildrenCount(element);

        for (var i = 0; i < count; i++)
        {
            CollectTabStops(VisualTreeHelper.GetChild(element, i), stops);
        }
    }

    private static string Describe(DependencyObject element)
    {
        var name = element is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name) ? fe.Name : string.Empty;
        var automation = AutomationProperties.GetName(element);
        var enabled = element is Control { IsEnabled: false } ? " DISABLED" : string.Empty;

        var label = !string.IsNullOrEmpty(automation) ? automation : name;

        return $"{element.GetType().Name}({label}){enabled}";
    }

    public static void Trace(string message)
    {
        if (Read("MC_TRACE") is not { } path)
        {
            return;
        }

        try
        {
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // A trace that cannot be written must not take the app with it.
        }
    }

    /// <summary>
    /// Invokes the error template's Retry button through its own binding.
    ///
    /// This is the one thing a screenshot cannot check: a Retry whose Command binding resolves to
    /// null renders enabled, reports a successful invoke and does nothing. The probe walks to the
    /// live button, reports whether a command is actually attached, executes it, and then reports
    /// whether the service was re-asked.
    /// </summary>
    public static async Task ProbeRetryAsync(DependencyObject root, SimulatedDriveHub hub)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));

        var button = FindByName<Button>(root, "RetryButton");

        if (button is null)
        {
            Trace("PROBE retry: no Retry button in the tree (is the app in the error state?)");
            return;
        }

        var command = button.Command;
        Trace($"PROBE retry: button found, Command={(command is null ? "NULL - dead button" : command.GetType().Name)}, " +
              $"CanExecute={command?.CanExecute(button.CommandParameter)}, IsEnabled={button.IsEnabled}");

        if (command is null)
        {
            return;
        }

        command.Execute(button.CommandParameter);
        await Task.Delay(TimeSpan.FromSeconds(3));

        // The outcome that matters is observable on screen: the failure panel is hidden again.
        // Hub revision is the wrong signal, because a successful read changes no state, and so is
        // the button's presence, because it lives in the value template either way.
        var panel = FindByName<FrameworkElement>(root, "ErrorPanel");
        var stillBroken = panel is null || panel.Visibility == Visibility.Visible;

        Trace($"PROBE retry: error panel visibility={panel?.Visibility.ToString() ?? "not found"}, " +
              $"faulted={hub.State.Faulted}, mode={hub.State.Mode}");
        Trace(stillBroken
            ? "PROBE retry: FAILED - the card is still showing the error"
            : "PROBE retry: PASSED - the card recovered to its live state");
    }

    private static T? FindByName<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        if (root is T match && match.Name == name)
        {
            return match;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            if (FindByName<T>(VisualTreeHelper.GetChild(root, i), name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
#endif


