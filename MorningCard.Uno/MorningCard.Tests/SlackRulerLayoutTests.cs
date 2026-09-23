using MorningCard.Core;
using MorningCard.Core.Models;
using Xunit;

namespace MorningCard.Tests;

/// <summary>
/// The v6 artboards' three ruler states (on time 7:24, leave now 7:33, late 7:37) with a 7:50 bell,
/// a 14-minute drive and a 3-minute walk, plus the label and clamping edges.
/// </summary>
public class SlackRulerLayoutTests
{
    private const int WalkMin = 3;

    private static readonly TimeOnly Bell = new(7, 50);

    private static SlackRulerPlan Plan(TimeOnly now, int delayMin = 0)
    {
        var trip = new TripPlan("School drop-off", Bell, 14, 16, delayMin, WalkMin);
        return SlackRulerLayout.Compute(TripMath.Derive(trip, now), WalkMin, Bell, now);
    }

    private static (int Start, int End) Range(SlackRulerPlan plan, RulerSegmentKind kind)
    {
        var segment = plan.Find(kind);
        Assert.NotNull(segment);
        return (segment.StartMin, segment.EndMin);
    }

    [Fact]
    public void Window_starts_thirty_minutes_before_the_bell()
    {
        var plan = Plan(new TimeOnly(7, 24));

        Assert.Equal(new TimeOnly(7, 20), plan.WindowStart);
        Assert.Equal(36, plan.WindowMinutes);
    }

    [Fact]
    public void On_time_matches_the_artboard()
    {
        var plan = Plan(new TimeOnly(7, 24));

        Assert.Equal(4, plan.NowMin);
        Assert.False(plan.LeaveIsNow);
        Assert.Equal(0, plan.LateByMin);
        Assert.Equal((0, 4), Range(plan, RulerSegmentKind.Past));
        Assert.Equal((4, 13), Range(plan, RulerSegmentKind.Slack));
        Assert.Equal((13, 27), Range(plan, RulerSegmentKind.Drive));
        Assert.Equal((27, 30), Range(plan, RulerSegmentKind.Walk));
        Assert.Equal((30, 36), Range(plan, RulerSegmentKind.After));
        Assert.Null(plan.Find(RulerSegmentKind.Overrun));

        Assert.Equal(new RulerKeyTime(13, new TimeOnly(7, 33), KeyTimeAlign.Center, true), plan.Leave);
        Assert.Equal(new RulerKeyTime(27, new TimeOnly(7, 47), KeyTimeAlign.Right, true), plan.Arrive);
        Assert.Equal(new RulerKeyTime(30, Bell, KeyTimeAlign.Left, true), plan.Bell);
    }

    [Fact]
    public void Leave_now_has_no_slack_and_hides_the_leave_line()
    {
        var plan = Plan(new TimeOnly(7, 33));

        Assert.True(plan.LeaveIsNow);
        Assert.Null(plan.Find(RulerSegmentKind.Slack));
        Assert.Equal((0, 13), Range(plan, RulerSegmentKind.Past));
        Assert.Equal((13, 27), Range(plan, RulerSegmentKind.Drive));
        Assert.Equal(13, plan.Leave.AtMin);
        Assert.False(plan.Leave.ShowLine);
    }

    [Fact]
    public void Late_overruns_the_bell_and_swaps_the_captions()
    {
        var plan = Plan(new TimeOnly(7, 37));

        Assert.True(plan.LeaveIsNow);
        Assert.Equal(4, plan.LateByMin);
        Assert.Equal((0, 17), Range(plan, RulerSegmentKind.Past));
        Assert.Equal((17, 31), Range(plan, RulerSegmentKind.Drive));
        Assert.Equal((31, 34), Range(plan, RulerSegmentKind.Walk));
        Assert.Equal((34, 36), Range(plan, RulerSegmentKind.After));
        Assert.Equal((30, 34), Range(plan, RulerSegmentKind.Overrun));

        Assert.Equal(new TimeOnly(7, 51), plan.Arrive.Time);
        Assert.Equal(KeyTimeAlign.Left, plan.Arrive.Align);
        Assert.Equal(KeyTimeAlign.Right, plan.Bell.Align);
    }

    [Fact]
    public void Traffic_delay_moves_the_leave_time_earlier()
    {
        var plan = Plan(new TimeOnly(7, 24), delayMin: 3);

        Assert.Equal((4, 10), Range(plan, RulerSegmentKind.Slack));
        Assert.Equal((10, 27), Range(plan, RulerSegmentKind.Drive));
        Assert.Equal(new TimeOnly(7, 30), plan.Leave.Time);
    }

    [Theory]
    [InlineData(31, RulerLabelLevel.None)]
    [InlineData(30, RulerLabelLevel.Short)]
    [InlineData(27, RulerLabelLevel.Short)]
    [InlineData(26, RulerLabelLevel.Full)]
    public void Slack_label_level_follows_its_length(int nowMinute, RulerLabelLevel expected)
    {
        var plan = Plan(new TimeOnly(7, nowMinute));

        Assert.Equal(expected, plan.Find(RulerSegmentKind.Slack)!.Label);
    }

    [Fact]
    public void The_three_minute_walk_gets_the_short_label()
    {
        Assert.Equal(RulerLabelLevel.Short, Plan(new TimeOnly(7, 24)).Find(RulerSegmentKind.Walk)!.Label);
    }

    [Fact]
    public void A_clock_before_the_window_clamps_to_its_left_edge()
    {
        var plan = Plan(new TimeOnly(7, 10));

        Assert.Equal(0, plan.NowMin);
        Assert.Null(plan.Find(RulerSegmentKind.Past));

        var slack = plan.Find(RulerSegmentKind.Slack)!;
        Assert.Equal((0, 13), (slack.StartMin, slack.EndMin));
        Assert.Equal(23, slack.LengthMin);
        Assert.Equal(RulerLabelLevel.Full, slack.Label);
    }

    [Fact]
    public void A_very_late_start_clamps_to_the_right_edge()
    {
        var plan = Plan(new TimeOnly(7, 45));

        Assert.Equal(12, plan.LateByMin);
        Assert.Equal((25, 36), Range(plan, RulerSegmentKind.Drive));
        Assert.Null(plan.Find(RulerSegmentKind.Walk));
        Assert.Null(plan.Find(RulerSegmentKind.After));
        Assert.Equal((30, 36), Range(plan, RulerSegmentKind.Overrun));
        Assert.Equal(36, plan.Arrive.AtMin);
    }
}
