using MorningCard.Core.Models;

namespace MorningCard.Core;

/// <summary>What a slack-ruler block stands for.</summary>
public enum RulerSegmentKind
{
    /// <summary>Before now: hatched.</summary>
    Past,

    /// <summary>Now to the leave time: the room you have.</summary>
    Slack,

    Drive,

    Walk,

    /// <summary>After the walk ends: hatched.</summary>
    After,

    /// <summary>Bell to the end of the walk, drawn over the other blocks when late.</summary>
    Overrun,
}

/// <summary>How much of a segment's label fits: the full text, a short word, or nothing.</summary>
public enum RulerLabelLevel
{
    None,
    Short,
    Full,
}

/// <summary>Which side of its line a key time's caption and value sit on.</summary>
public enum KeyTimeAlign
{
    Center,

    /// <summary>Text starts just right of the line.</summary>
    Left,

    /// <summary>Text ends just left of the line.</summary>
    Right,
}

/// <param name="StartMin">Minutes from the window start, clamped to the window.</param>
/// <param name="EndMin">Minutes from the window start, clamped to the window.</param>
/// <param name="LengthMin">The segment's real length, before clamping; labels read this.</param>
public sealed record RulerSegment(RulerSegmentKind Kind, int StartMin, int EndMin, int LengthMin, RulerLabelLevel Label);

/// <param name="AtMin">Minutes from the window start, clamped to the window.</param>
/// <param name="Time">The clock time the key shows.</param>
/// <param name="ShowLine">False for the leave line once leaving is now: the now line takes its place.</param>
public sealed record RulerKeyTime(int AtMin, TimeOnly Time, KeyTimeAlign Align, bool ShowLine);

/// <param name="WindowStart">The clock time at the ruler's left edge.</param>
/// <param name="WindowMinutes">The ruler's span; one minor tick per minute, a major one every five.</param>
/// <param name="NowMin">Minutes from the window start, clamped to the window.</param>
/// <param name="LeaveIsNow">No slack left: the leave key reads "Now" and the now dot is hidden.</param>
/// <param name="LateByMin">Minutes the walk ends after the bell; 0 when on time.</param>
public sealed record SlackRulerPlan(
    TimeOnly WindowStart,
    int WindowMinutes,
    int NowMin,
    bool LeaveIsNow,
    int LateByMin,
    IReadOnlyList<RulerSegment> Segments,
    RulerKeyTime Leave,
    RulerKeyTime Arrive,
    RulerKeyTime Bell)
{
    public RulerSegment? Find(RulerSegmentKind kind) => Segments.FirstOrDefault(segment => segment.Kind == kind);
}

/// <summary>
/// The v6 slack ruler as minute ranges: pure, so every state the card can show is reachable from a
/// test. Ported from the design's ruler (fixed 36-minute window ending six minutes after the bell,
/// one tick per minute), with the window anchored to the bell instead of hard-coded at 7:20.
/// </summary>
public static class SlackRulerLayout
{
    /// <summary>Minutes of window before the bell. 30 puts a 7:50 bell's window start at 7:20.</summary>
    public const int MinutesBeforeBell = 30;

    public const int DefaultWindowMinutes = 36;

    /// <summary>A segment at least this long shows its full label ("Slack · 9 min").</summary>
    public const int FullLabelMin = 7;

    /// <summary>A segment at least this long shows its short label ("Slack").</summary>
    public const int ShortLabelMin = 3;

    public static SlackRulerPlan Compute(
        TripTimeline timeline,
        int walkMin,
        TimeOnly bell,
        TimeOnly now,
        int windowMinutes = DefaultWindowMinutes)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var windowStart = TripMath.FromMinutes(TripMath.ToMinutes(bell) - MinutesBeforeBell);
        var origin = TripMath.ToMinutes(windowStart);
        int At(TimeOnly time) => TripMath.ToMinutes(time) - origin;

        var nowAt = At(now);
        var leaveAt = At(timeline.LeaveBy);
        var startAt = At(timeline.Start);
        var arriveAt = At(timeline.Arrive);
        var walkEndAt = arriveAt + walkMin;
        var bellAt = At(bell);
        var lateBy = Math.Max(0, walkEndAt - bellAt);
        var leaveIsNow = timeline.SlackMin <= 0;

        int Clamp(int minutes) => Math.Clamp(minutes, 0, windowMinutes);

        var segments = new List<RulerSegment>();

        void Add(RulerSegmentKind kind, int start, int end)
        {
            var length = end - start;
            var (from, to) = (Clamp(start), Clamp(end));

            if (length > 0 && to > from)
            {
                segments.Add(new RulerSegment(kind, from, to, length, LabelFor(length)));
            }
        }

        Add(RulerSegmentKind.Past, 0, nowAt);
        Add(RulerSegmentKind.Slack, nowAt, leaveAt);
        Add(RulerSegmentKind.Drive, startAt, arriveAt);
        Add(RulerSegmentKind.Walk, arriveAt, walkEndAt);
        Add(RulerSegmentKind.After, walkEndAt, windowMinutes);
        Add(RulerSegmentKind.Overrun, bellAt, walkEndAt);

        // ARRIVE and BELL face away from each other so neither caption overlaps the other; when
        // late, ARRIVE lands right of BELL and the two swap sides.
        var arriveFirst = arriveAt < bellAt;

        return new SlackRulerPlan(
            WindowStart: windowStart,
            WindowMinutes: windowMinutes,
            NowMin: Clamp(nowAt),
            LeaveIsNow: leaveIsNow,
            LateByMin: lateBy,
            Segments: segments,
            Leave: new RulerKeyTime(Clamp(startAt), timeline.Start, KeyTimeAlign.Center, ShowLine: !leaveIsNow),
            Arrive: new RulerKeyTime(Clamp(arriveAt), timeline.Arrive, arriveFirst ? KeyTimeAlign.Right : KeyTimeAlign.Left, ShowLine: true),
            Bell: new RulerKeyTime(Clamp(bellAt), bell, arriveFirst ? KeyTimeAlign.Left : KeyTimeAlign.Right, ShowLine: true));
    }

    public static RulerLabelLevel LabelFor(int lengthMin) => lengthMin switch
    {
        >= FullLabelMin => RulerLabelLevel.Full,
        >= ShortLabelMin => RulerLabelLevel.Short,
        _ => RulerLabelLevel.None,
    };
}
