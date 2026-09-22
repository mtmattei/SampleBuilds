using MorningCard.Core;
using MorningCard.Core.Models;
using Xunit;

namespace MorningCard.Tests;

public class RouteLayoutTests
{
    private static TripPlan Plan(int delayMin = 0, int walkMin = 3) =>
        new("School drop-off", new TimeOnly(7, 50), 14, 16, delayMin, walkMin);

    private static RouteFractions Layout(TimeOnly now, int delayMin = 0, int walkMin = 3)
    {
        var plan = Plan(delayMin, walkMin);
        return RouteLayout.Fractions(TripMath.Derive(plan, now), plan.WalkMin);
    }

    [Fact]
    public void Default_morning_splits_the_path_by_minutes()
    {
        var fractions = Layout(new TimeOnly(7, 24));

        // 9 slack + 14 drive + 3 walk = 26 minutes along the path.
        Assert.Equal(9, fractions.SlackVisibleMin);
        Assert.Equal(9d / 26d, fractions.Leave, 6);
        Assert.Equal(23d / 26d, fractions.Arrive, 6);
    }

    [Fact]
    public void Stretches_stay_in_order_and_inside_the_path()
    {
        var fractions = Layout(new TimeOnly(7, 24));

        Assert.True(fractions.Leave >= 0d);
        Assert.True(fractions.Leave < fractions.Arrive);
        Assert.True(fractions.Arrive <= 1d);
    }

    [Fact]
    public void Slack_is_capped_so_a_long_wait_cannot_crush_the_drive()
    {
        // 29 minutes of slack, well past the cap.
        var fractions = Layout(new TimeOnly(7, 4));

        Assert.Equal(RouteLayout.SlackCapMin, fractions.SlackVisibleMin);
        Assert.Equal(12d / 29d, fractions.Leave, 6);
    }

    [Fact]
    public void Running_late_removes_the_slack_stretch_entirely()
    {
        var fractions = Layout(new TimeOnly(7, 40));

        Assert.Equal(0, fractions.SlackVisibleMin);
        Assert.Equal(0d, fractions.Leave, 6);
        Assert.Equal(14d / 17d, fractions.Arrive, 6);
    }

    [Fact]
    public void Leaving_exactly_on_time_also_removes_the_slack_stretch()
    {
        var fractions = Layout(new TimeOnly(7, 33));

        Assert.Equal(0, fractions.SlackVisibleMin);
        Assert.Equal(0d, fractions.Leave, 6);
    }

    [Fact]
    public void A_heavier_delay_takes_a_bigger_share_of_the_path()
    {
        var light = Layout(new TimeOnly(7, 24));
        var heavy = Layout(new TimeOnly(7, 24), delayMin: 8);

        var lightDrive = light.Arrive - light.Leave;
        var heavyDrive = heavy.Arrive - heavy.Leave;

        Assert.True(heavyDrive > lightDrive);
    }

    [Fact]
    public void A_zero_length_trip_degenerates_to_a_whole_drive_rather_than_dividing_by_zero()
    {
        var plan = new TripPlan("Nothing", new TimeOnly(7, 50), 0, 0, 0, 0);
        var timeline = TripMath.Derive(plan, plan.Bell);

        var fractions = RouteLayout.Fractions(timeline, plan.WalkMin);

        Assert.Equal(0d, fractions.Leave, 6);
        Assert.Equal(1d, fractions.Arrive, 6);
    }
}
