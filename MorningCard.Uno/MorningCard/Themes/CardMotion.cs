using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace MorningCard.Themes;

/// <summary>
/// One signal for "play the entrance again", raised by the Simulator and handled by the card.
/// Separate from <see cref="CardChrome"/> because it is an event, not a value.
/// </summary>
public static class CardMotion
{
    public static event EventHandler? ReplayRequested;

    public static void RequestReplay() => ReplayRequested?.Invoke(null, EventArgs.Empty);

    /// <summary>
    /// A curve from MotionTokens, so C#-built animations and XAML ones share one definition.
    /// </summary>
    /// <remarks>
    /// Falls back to the house default rather than throwing: a missing curve should cost the feel,
    /// never the frame. Unlike the theme brushes, these carry no colour, so there is no theme to
    /// resolve against and an application-level lookup is correct here.
    /// </remarks>
    public static KeySpline Ease(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is KeySpline spline
            ? spline
            : new KeySpline { ControlPoint1 = new Point(0.22, 1), ControlPoint2 = new Point(0.36, 1) };

    /// <summary>A duration or other number from MotionTokens.</summary>
    public static double Number(string key, double fallback) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is double number
            ? number
            : fallback;
}
