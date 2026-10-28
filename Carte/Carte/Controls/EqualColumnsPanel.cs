using Windows.Foundation;

namespace Carte.Controls;

// Gives every child the same width across one row, like the category tabs in the design.
// When equal columns would clip a label (narrow windows, larger text), columns follow content width instead.
public sealed partial class EqualColumnsPanel : Panel
{
    private double[] _widths = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        if (count == 0)
        {
            _widths = [];
            return new Size(0, 0);
        }
        var natural = new double[count];
        double height = 0;
        for (var i = 0; i < count; i++)
        {
            Children[i].Measure(new Size(double.PositiveInfinity, availableSize.Height));
            natural[i] = Children[i].DesiredSize.Width;
            height = Math.Max(height, Children[i].DesiredSize.Height);
        }

        var available = double.IsInfinity(availableSize.Width) ? natural.Sum() : availableSize.Width;
        var equal = count == 0 ? 0 : available / count;
        var spare = Math.Max(0, available - natural.Sum());
        _widths = natural.Max() <= equal
            ? natural.Select(_ => equal).ToArray()
            : natural.Select(w => w + spare / count).ToArray();

        for (var i = 0; i < count; i++)
            Children[i].Measure(new Size(_widths[i], availableSize.Height));
        return new Size(available, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        for (var i = 0; i < Children.Count && i < _widths.Length; i++)
        {
            Children[i].Arrange(new Rect(x, 0, _widths[i], finalSize.Height));
            x += _widths[i];
        }
        return finalSize;
    }
}
