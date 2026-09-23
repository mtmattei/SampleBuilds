namespace MorningCard.Presentation;

/// <summary>
/// The one simulated hub in the process. The Android Auto session can start without the phone UI
/// (and so without the app host) ever launching, and it has to show the same trip, so the host
/// registers this instance rather than building its own.
/// </summary>
public static class SharedHub
{
    public static SimulatedDriveHub Instance { get; } = new(TimeProvider.System);
}
