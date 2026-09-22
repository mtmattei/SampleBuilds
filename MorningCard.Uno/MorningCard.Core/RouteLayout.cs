using MorningCard.Core.Models;

namespace MorningCard.Core;

/// <summary>
/// Turns the timeline into positions along the route path, ported from the prototype's
/// <c>drawRoute()</c>. This is the signature device: minutes become distance along the path, so the
/// wait before leaving is as visible as the drive itself.
/// </summary>
public static class RouteLayout
{
    /// <summary>
    /// Slack is drawn up to this many minutes. Past it the dotted stretch would crowd the drive
    /// segment off the map, and the exact length of a long wait is not what the glance is for.
    /// </summary>
    public const int SlackCapMin = 12;

    public static RouteFractions Fractions(TripTimeline timeline, int walkMin)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        // Running late means the car leaves now: there is no slack stretch to draw.
        var slackVisible = timeline.IsLate ? 0 : Math.Min(timeline.SlackMin, SlackCapMin);
        var total = slackVisible + timeline.DriveMin + walkMin;

        if (total <= 0)
        {
            return new RouteFractions(0d, 1d, 0);
        }

        return new RouteFractions(
            Leave: (double)slackVisible / total,
            Arrive: (double)(slackVisible + timeline.DriveMin) / total,
            SlackVisibleMin: slackVisible);
    }
}
