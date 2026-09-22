namespace MorningCard.Core.Models;

/// <summary>
/// Where the three stretches of the ruler fall along the route path, as fractions of its length.
/// Minutes become distance: slack runs from 0 to <paramref name="Leave"/>, the drive from there to
/// <paramref name="Arrive"/>, and the walk from there to the end.
/// </summary>
/// <param name="SlackVisibleMin">
/// The slack actually drawn. Capped so a long wait cannot squeeze the drive segment down to nothing,
/// and zero when late.
/// </param>
public record RouteFractions(double Leave, double Arrive, int SlackVisibleMin);
