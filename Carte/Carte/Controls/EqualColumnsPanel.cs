using Windows.Foundation;

namespace Carte.Controls;

// Gives every child the same width across one row, like the category tabs in the design.
public sealed partial class EqualColumnsPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Math.Max(1, Children.Count);
        var columnWidth = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width / count;
        double height = 0, widest = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(columnWidth, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? widest * count : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnWidth = finalSize.Width / Math.Max(1, Children.Count);
        for (var i = 0; i < Children.Count; i++)
            Children[i].Arrange(new Rect(i * columnWidth, 0, columnWidth, finalSize.Height));
        return finalSize;
    }
}
