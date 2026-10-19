using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>
/// The band of labels above the slack ruler. Every label anchors to an edge on the track and sits
/// above it. A label that would meet another slides along the row, away from it; only when the row
/// has no room does it go up a tier. Sliding first keeps the band one row tall as the clock runs,
/// so the track below it does not jump each time two labels pass each other.
///
/// Children, in order, each with its anchor:
///   0  the callout      left edge at Now (pulled out by <see cref="CalloutInset"/> so its dot
///                       sits on the now line)
///   1  Leave            left edge where the drive starts
///   2  Arrive           right edge where the drive ends
///   3  Bell             right edge at the bell, allowed to run into the end stub
///
/// The edges are computed from the same weights and fixed sizes as the track's Grid columns
/// (stub, slack*, drive*, walk*, stub with a column gap), so the labels line up with the blocks
/// without measuring the blocks themselves.
/// </summary>
public sealed partial class RulerLabelPanel : Panel
{
    public static readonly DependencyProperty SlackWeightProperty = Weight(nameof(SlackWeight));
    public static readonly DependencyProperty DriveWeightProperty = Weight(nameof(DriveWeight));
    public static readonly DependencyProperty WalkWeightProperty = Weight(nameof(WalkWeight));

    public double SlackWeight { get => (double)GetValue(SlackWeightProperty); set => SetValue(SlackWeightProperty, value); }
    public double DriveWeight { get => (double)GetValue(DriveWeightProperty); set => SetValue(DriveWeightProperty, value); }
    public double WalkWeight { get => (double)GetValue(WalkWeightProperty); set => SetValue(WalkWeightProperty, value); }

    /// <summary>The track's end stubs, matching its first and last Grid columns.</summary>
    public double StubWidth { get; set; } = 12;

    /// <summary>The track Grid's ColumnSpacing.</summary>
    public double ColumnGap { get; set; } = 2;

    /// <summary>How far the callout reaches left of the now line.</summary>
    public double CalloutInset { get; set; } = 10;

    /// <summary>The least space between two labels on the same tier.</summary>
    public double LabelGap { get; set; } = 8;

    /// <summary>Space between tiers.</summary>
    public double TierSpacing { get; set; } = 4;

    private readonly List<(Rect Slot, int Tier)> _placed = [];
    private readonly List<double> _tierHeights = [];

    private static DependencyProperty Weight(string name) => DependencyProperty.Register(
        name,
        typeof(double),
        typeof(RulerLabelPanel),
        new PropertyMetadata(1d, (d, _) => ((RulerLabelPanel)d).InvalidateMeasure()));

    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(unbounded);
        }

        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        Place(width);

        var height = _tierHeights.Sum() + TierSpacing * Math.Max(0, _tierHeights.Count - 1);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Place(finalSize.Width);

        // Tier 0 sits on the track; higher tiers stack upward from it.
        var bottoms = new double[_tierHeights.Count];
        var y = finalSize.Height;
        for (var t = 0; t < _tierHeights.Count; t++)
        {
            bottoms[t] = y;
            y -= _tierHeights[t] + TierSpacing;
        }

        for (var i = 0; i < _placed.Count; i++)
        {
            var (slot, tier) = _placed[i];
            Children[i].Arrange(new Rect(slot.X, bottoms[tier] - slot.Height, slot.Width, slot.Height));
        }

        return finalSize;
    }

    private void Place(double width)
    {
        _placed.Clear();
        _tierHeights.Clear();

        var (now, leave, arrive, bell) = Edges(width);
        var anchors = new (double X, bool RightAligned)[]
        {
            (now - CalloutInset, false),
            (leave, false),
            (arrive, true),
            (width, true),
        };

        for (var i = 0; i < Children.Count; i++)
        {
            _placed.Add((new Rect(0, 0, 0, 0), -1));
        }

        // The callout and Bell hold their anchors exactly: they are Now and the deadline. Leave and
        // Arrive are placed after them and give way sideways.
        foreach (var i in PlacementOrder)
        {
            if (i >= Children.Count)
            {
                continue;
            }

            var size = Children[i].DesiredSize;
            if (Children[i].Visibility == Visibility.Collapsed || size.Width <= 0)
            {
                continue;
            }

            var (x, right) = i < anchors.Length ? anchors[i] : (0d, false);
            var preferred = Math.Clamp(right ? x - size.Width : x, 0, Math.Max(0, width - size.Width));

            // Slide first, lift last. A label that meets another moves along the row, away from the
            // one it met (a left-anchored label to the right, a right-anchored one to the left), so as
            // the clock runs the labels drift with their edges instead of jumping a whole row. Only
            // when the row has no room left does the label go up a tier.
            var left = preferred;
            var tier = 0;
            while (Blocker(left, left + size.Width, 0) is { } blocker)
            {
                left = right ? blocker.Left - LabelGap - size.Width : blocker.Right + LabelGap;
                if (left < 0 || left + size.Width > width)
                {
                    left = preferred;
                    tier = 1;
                    while (Blocker(left, left + size.Width, tier) is not null)
                    {
                        tier++;
                    }

                    break;
                }
            }

            while (_tierHeights.Count <= tier)
            {
                _tierHeights.Add(0);
            }

            _tierHeights[tier] = Math.Max(_tierHeights[tier], size.Height);
            _placed[i] = (new Rect(left, 0, size.Width, size.Height), tier);
        }

        for (var i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Tier < 0)
            {
                _placed[i] = (_placed[i].Slot, 0);
            }
        }
    }

    /// <summary>Callout, Bell, then Leave, then Arrive.</summary>
    private static readonly int[] PlacementOrder = [0, 3, 1, 2];

    /// <summary>The placed label this span would touch on the tier, if any.</summary>
    private Rect? Blocker(double left, double right, int tier)
    {
        foreach (var (slot, t) in _placed)
        {
            if (t == tier && slot.Width > 0 && left < slot.Right + LabelGap && right + LabelGap > slot.Left)
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>The now line, the drive's two edges and the bell, as x in this panel.</summary>
    private (double Now, double Leave, double Arrive, double Bell) Edges(double width)
    {
        var total = Math.Max(SlackWeight + DriveWeight + WalkWeight, double.Epsilon);
        var track = Math.Max(0, width - 2 * StubWidth - 4 * ColumnGap);

        var now = StubWidth + ColumnGap;
        var leave = now + track * SlackWeight / total + ColumnGap;
        var arrive = leave + track * DriveWeight / total;
        var bell = arrive + ColumnGap + track * WalkWeight / total;
        return (now, leave, arrive, bell);
    }
}
