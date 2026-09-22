using MorningCard.Core.Models;

namespace MorningCard.Core;

/// <summary>
/// The departure arithmetic behind the card, ported from the prototype's <c>derive()</c>. Pure and
/// clock-injected so every state the card can show is reachable from a test.
/// </summary>
public static class TripMath
{
    public const int MinutesPerDay = 24 * 60;

    /// <summary>Slack at or above this shows a countdown; below it the card says "leave now".</summary>
    public const int LeaveNowThresholdMin = 2;

    /// <summary>Range warns below this, in kilometres.</summary>
    public const int RangeWarnKm = 110;

    /// <summary>Range is bad below this, in kilometres.</summary>
    public const int RangeBadKm = 50;

    public static TripTimeline Derive(TripPlan plan, TimeOnly now)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var nowMin = ToMinutes(now);
        var bellMin = ToMinutes(plan.Bell);

        var drive = plan.BaseDriveMin + plan.DelayMin;
        var leaveByMin = bellMin - plan.WalkMin - drive;
        var slack = leaveByMin - nowMin;
        var isLate = slack < 0;

        // Once you are late the drive starts now, not at a leave-by that has already passed.
        var startMin = isLate ? nowMin : leaveByMin;
        var arriveMin = startMin + drive;

        return new TripTimeline(
            DriveMin: drive,
            LeaveBy: FromMinutes(leaveByMin),
            SlackMin: slack,
            IsLate: isLate,
            Start: FromMinutes(startMin),
            Arrive: FromMinutes(arriveMin),
            TotalMin: drive + plan.WalkMin,
            Tone: ToneFor(slack),
            Traffic: TrafficFor(plan.DelayMin),
            VsUsualMin: plan.UsualDriveMin - drive);
    }

    public static CardTone ToneFor(int slackMin) => slackMin switch
    {
        < 0 => CardTone.Late,
        < LeaveNowThresholdMin => CardTone.LeaveNow,
        _ => CardTone.Ok,
    };

    public static TrafficLevel TrafficFor(int delayMin) => delayMin switch
    {
        <= 1 => TrafficLevel.Light,
        <= 6 => TrafficLevel.Moderate,
        _ => TrafficLevel.Heavy,
    };

    public static StatLevel RangeLevel(int rangeMi) => rangeMi switch
    {
        < RangeBadKm => StatLevel.Bad,
        < RangeWarnKm => StatLevel.Warn,
        _ => StatLevel.Normal,
    };

    /// <summary>Minutes since midnight.</summary>
    public static int ToMinutes(TimeOnly time) => (time.Hour * 60) + time.Minute;

    /// <summary>
    /// Builds a <see cref="TimeOnly"/> from minutes since midnight, wrapping across midnight so an
    /// early bell or a long drive cannot throw.
    /// </summary>
    public static TimeOnly FromMinutes(int minutes)
    {
        var wrapped = ((minutes % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
        return new TimeOnly(wrapped / 60, wrapped % 60);
    }
}
