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
/// <param name="DestinationLabel">What the destination row is called, e.g. "At school".</param>
/// <param name="ParkingStreet">Where the car ends up, e.g. "Rue Rachel Est".</param>
/// <param name="ParkingNote">
/// The restriction that matters on arrival, e.g. "No parking from 9:30". Part of the trip rather
/// than the vehicle: it is a property of where this trip ends, not of the car.
/// </param>
public record TripPlan(
    string Title,
    TimeOnly Bell,
    int BaseDriveMin,
    int UsualDriveMin,
    int DelayMin,
    int WalkMin,
    // Defaulted: these three describe where the trip ends, and none of them takes part in the
    // departure arithmetic - so the maths tests can keep constructing a plan from times alone.
    string DestinationLabel = "",
    string ParkingStreet = "",
    string ParkingNote = "");
