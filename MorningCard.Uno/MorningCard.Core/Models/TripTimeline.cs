namespace MorningCard.Core.Models;

/// <summary>
/// Everything the card says about departure, derived from a <see cref="TripPlan"/> and the clock.
/// </summary>
/// <param name="DriveMin">Base drive plus today's delay.</param>
/// <param name="LeaveBy">The last moment you can leave and still be inside for the bell.</param>
/// <param name="SlackMin">Minutes between now and <paramref name="LeaveBy"/>; negative when late.</param>
/// <param name="IsLate">True when leaving right now still arrives after the bell.</param>
/// <param name="Start">When the drive starts: <paramref name="LeaveBy"/>, or now when late.</param>
/// <param name="Arrive">When the car reaches the school.</param>
/// <param name="TotalMin">Drive plus walk, the big number on the card.</param>
/// <param name="VsUsualMin">Positive when faster than usual, negative when slower.</param>
public record TripTimeline(
    int DriveMin,
    TimeOnly LeaveBy,
    int SlackMin,
    bool IsLate,
    TimeOnly Start,
    TimeOnly Arrive,
    int TotalMin,
    CardTone Tone,
    TrafficLevel Traffic,
    int VsUsualMin);
