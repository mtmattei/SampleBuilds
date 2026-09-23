namespace MorningCard.Core.Models;

/// <summary>How heavy traffic is, derived from the delay against the usual drive.</summary>
public enum TrafficLevel
{
    Light,
    Moderate,
    Heavy,
}

/// <summary>What the card is saying about the departure, which drives colour as well as copy.</summary>
public enum CardTone
{
    /// <summary>Two minutes or more of slack.</summary>
    Ok,

    /// <summary>Under two minutes of slack: leave now to arrive on time.</summary>
    LeaveNow,

    /// <summary>Negative slack: leaving now still arrives after the bell.</summary>
    Late,
}

/// <summary>Severity of a single vehicle stat.</summary>
public enum StatLevel
{
    Normal,
    Warn,
    Bad,
}

/// <summary>What the simulated car and trip services are pretending to be doing.</summary>
public enum ConnectionMode
{
    /// <summary>Everything reachable, data flowing.</summary>
    Live,

    /// <summary>The car is unreachable; the last snapshot is served with a stale marker.</summary>
    Offline,

    /// <summary>Nothing on the calendar, so there is no trip to plan.</summary>
    NoTrip,

    /// <summary>
    /// The plan is known but the live ETA has not answered yet. Only meaningful from startup
    /// (MC_STATE=loading): once an ETA has been shown, a later lookup keeps it on screen rather than
    /// blanking it, so switching into this mode mid-session would change nothing visible.
    /// </summary>
    Loading,
}
