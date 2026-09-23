namespace MorningCard.Presentation;

/// <summary>
/// Simulator-to-card requests that are not hub state (v6 decision D6: Refresh and Replay live in the
/// Simulator). Replay goes through <see cref="Themes.CardMotion"/>; Refresh comes here, and the card's
/// model answers it with its own Retry.
/// </summary>
public static class CardSignals
{
    public static event EventHandler? RefreshRequested;

    public static void RequestRefresh() => RefreshRequested?.Invoke(null, EventArgs.Empty);
}
