namespace Carte.Core;

// Words for a placement, built only from the category's own axis labels.
// Used for automation names so the map is never the only route to an item's taste.
public static class TasteDescriber
{
    private const double LowBand = 0.4;
    private const double HighBand = 0.6;

    public static string Describe(TastePlacement placement, AxisSet axes)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(axes);
        var x = Word(placement.X, axes.X);
        var y = Word(placement.Y, axes.Y);
        return x.IsEnd && y.IsEnd ? $"{x.Text} and {y.Text}" : $"{x.Text}, {y.Text}";
    }

    public static string DescribeAxes(AxisSet axes) =>
        $"{axes.X.Low} to {axes.X.High}, {axes.Y.Low} to {axes.Y.High}";

    private static (string Text, bool IsEnd) Word(double value, AxisDefinition axis) => value switch
    {
        < LowBand => (axis.Low.ToLowerInvariant(), true),
        > HighBand => (axis.High.ToLowerInvariant(), true),
        _ => ($"between {axis.Low.ToLowerInvariant()} and {axis.High.ToLowerInvariant()}", false)
    };
}
