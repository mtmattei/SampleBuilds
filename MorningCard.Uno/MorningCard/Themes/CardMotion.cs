namespace MorningCard.Themes;

/// <summary>
/// One signal for "play the entrance again", raised by the Simulator and handled by the card.
/// Separate from <see cref="CardChrome"/> because it is an event, not a value.
/// </summary>
public static class CardMotion
{
    public static event EventHandler? ReplayRequested;

    public static void RequestReplay() => ReplayRequested?.Invoke(null, EventArgs.Empty);
}
