namespace MorningCard.Core.Models;

/// <summary>
/// The morning's trip as the services report it: when the bell rings, how long the drive normally
/// takes, how much traffic is adding today, and how long the walk in from the curb takes.
/// </summary>
/// <param name="Title">Display name of the trip, e.g. "School drop-off".</param>
/// <param name="Bell">When the child has to be inside.</param>
/// <param name="BaseDriveMin">Free-flow drive time.</param>
/// <param name="UsualDriveMin">What this drive normally takes, used for the "faster than usual" line.</param>
/// <param name="DelayMin">Minutes traffic is adding on top of <paramref name="BaseDriveMin"/>.</param>
/// <param name="WalkMin">
/// Walk from the drop-off curb to the door. Per-school, supplied with the trip rather than by a
/// routing service, which only knows about the drive.
/// </param>
public record TripPlan(
    string Title,
    TimeOnly Bell,
    int BaseDriveMin,
    int UsualDriveMin,
    int DelayMin,
    int WalkMin);
