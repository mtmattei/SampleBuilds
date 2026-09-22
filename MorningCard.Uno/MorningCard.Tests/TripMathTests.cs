using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Services;
using Xunit;

namespace MorningCard.Tests;

public class TripMathTests
{
    private static TripPlan Plan(int delayMin = 0, int baseDriveMin = 14, int usualDriveMin = 16, int walkMin = 3) =>
        new("School drop-off", new TimeOnly(7, 50), baseDriveMin, usualDriveMin, delayMin, walkMin);

    private static TimeOnly At(int hour, int minute) => new(hour, minute);

    [Fact]
    public void Default_morning_matches_the_mockup()
    {
        var timeline = TripMath.Derive(Plan(), At(7, 24));

        Assert.Equal(14, timeline.DriveMin);
        Assert.Equal(At(7, 33), timeline.LeaveBy);
        Assert.Equal(9, timeline.SlackMin);
        Assert.False(timeline.IsLate);
        Assert.Equal(At(7, 47), timeline.Arrive);
        Assert.Equal(17, timeline.TotalMin);
        Assert.Equal(CardTone.Ok, timeline.Tone);
        Assert.Equal(TrafficLevel.Light, timeline.Traffic);
        Assert.Equal(2, timeline.VsUsualMin);
    }

    [Theory]
    [InlineData(7, 30, 3, CardTone.Ok)]
    [InlineData(7, 31, 2, CardTone.Ok)]
    [InlineData(7, 32, 1, CardTone.LeaveNow)]
    [InlineData(7, 33, 0, CardTone.LeaveNow)]
    [InlineData(7, 34, -1, CardTone.Late)]
    public void Tone_turns_over_at_two_minutes_of_slack_and_again_at_zero(
        int hour, int minute, int expectedSlack, CardTone expectedTone)
    {
        var timeline = TripMath.Derive(Plan(), At(hour, minute));

        Assert.Equal(expectedSlack, timeline.SlackMin);
        Assert.Equal(expectedTone, timeline.Tone);
    }

    [Fact]
    public void Running_late_starts_the_drive_now_rather_than_at_a_leave_by_that_has_passed()
    {
        var timeline = TripMath.Derive(Plan(), At(7, 40));

        Assert.True(timeline.IsLate);
        Assert.Equal(-7, timeline.SlackMin);
        Assert.Equal(At(7, 40), timeline.Start);
        Assert.Equal(At(7, 54), timeline.Arrive);

        // Leave-by is still reported, even though it is in the past.
        Assert.Equal(At(7, 33), timeline.LeaveBy);
    }

    [Fact]
    public void Traffic_delay_pushes_leave_by_earlier_and_lengthens_the_drive()
    {
        var timeline = TripMath.Derive(Plan(delayMin: 7), At(7, 24));

        Assert.Equal(21, timeline.DriveMin);
        Assert.Equal(At(7, 26), timeline.LeaveBy);
        Assert.Equal(2, timeline.SlackMin);
        Assert.Equal(TrafficLevel.Heavy, timeline.Traffic);
        Assert.Equal(-5, timeline.VsUsualMin);
    }

    [Theory]
    [InlineData(0, TrafficLevel.Light)]
    [InlineData(1, TrafficLevel.Light)]
    [InlineData(2, TrafficLevel.Moderate)]
    [InlineData(6, TrafficLevel.Moderate)]
    [InlineData(7, TrafficLevel.Heavy)]
    public void Traffic_level_boundaries(int delayMin, TrafficLevel expected) =>
        Assert.Equal(expected, TripMath.TrafficFor(delayMin));

    [Theory]
    [InlineData(14, 2)]   // faster than usual
    [InlineData(16, 0)]   // about usual
    [InlineData(19, -3)]  // slower than usual
    public void Vs_usual_is_signed_against_the_usual_drive(int baseDriveMin, int expected)
    {
        var timeline = TripMath.Derive(Plan(baseDriveMin: baseDriveMin), At(7, 10));

        Assert.Equal(expected, timeline.VsUsualMin);
    }

    // Kilometres since v2: the thresholds moved with the units, so the boundary cases move too.
    [Theory]
    [InlineData(520, StatLevel.Normal)]
    [InlineData(110, StatLevel.Normal)]
    [InlineData(109, StatLevel.Warn)]
    [InlineData(50, StatLevel.Warn)]
    [InlineData(49, StatLevel.Bad)]
    [InlineData(0, StatLevel.Bad)]
    public void Range_thresholds(int rangeKm, StatLevel expected) =>
        Assert.Equal(expected, TripMath.RangeLevel(rangeKm));

    [Theory]
    [InlineData(33, StatLevel.Normal)]
    [InlineData(30, StatLevel.Normal)]
    [InlineData(29, StatLevel.Warn)]
    [InlineData(26, StatLevel.Warn)]
    [InlineData(25, StatLevel.Bad)]
    public void Tire_thresholds(int psi, StatLevel expected) =>
        Assert.Equal(expected, TripMath.TireLevel(psi));

    [Theory]
    [InlineData(320, 400, 80d)]
    [InlineData(400, 400, 100d)]
    [InlineData(0, 400, 2d)]    // floored so the bar never disappears
    [InlineData(4, 400, 2d)]    // 1% would round to nothing visible
    public void Range_bar_percentage_is_floored(int rangeMi, int rangeMaxMi, double expected) =>
        Assert.Equal(expected, TripMath.RangeBarPercent(rangeMi, rangeMaxMi), 3);

    [Fact]
    public void Range_bar_survives_a_zero_maximum()
    {
        Assert.Equal(TripMath.RangeBarFloorPercent, TripMath.RangeBarPercent(0, 0), 3);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(444, 7, 24)]
    [InlineData(1439, 23, 59)]
    public void Minutes_round_trip_through_time_of_day(int minutes, int hour, int minute)
    {
        var time = TripMath.FromMinutes(minutes);

        Assert.Equal(new TimeOnly(hour, minute), time);
        Assert.Equal(minutes, TripMath.ToMinutes(time));
    }

    [Theory]
    [InlineData(-30, 23, 30)]
    [InlineData(1500, 1, 0)]
    public void Minutes_wrap_across_midnight_rather_than_throwing(int minutes, int hour, int minute) =>
        Assert.Equal(new TimeOnly(hour, minute), TripMath.FromMinutes(minutes));

    [Fact]
    public void A_bell_just_after_midnight_wraps_instead_of_throwing()
    {
        var plan = new TripPlan("Night run", new TimeOnly(0, 10), 14, 16, 0, 3);

        var timeline = TripMath.Derive(plan, new TimeOnly(23, 30));

        Assert.Equal(new TimeOnly(23, 53), timeline.LeaveBy);
    }

    [Fact]
    public void The_hub_default_is_the_morning_the_mockup_draws()
    {
        var state = DriveHubState.Default;

        var timeline = TripMath.Derive(state.Trip, TripMath.FromMinutes(state.ClockMinutes));

        Assert.Equal(9, timeline.SlackMin);
        Assert.Equal(17, timeline.TotalMin);
        Assert.Equal(new TimeOnly(7, 47), timeline.Arrive);
    }
}
