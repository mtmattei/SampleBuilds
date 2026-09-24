using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>
/// The card's headline block: title, car and the one-line "Leave in" sentence.
///
/// Children, in order: the title, the car, the sentence. When the sentence fits at full size in the
/// column beside the car, title and sentence stack on the left and the car is centred on the right.
/// When it would not, the car moves up beside the title alone and the sentence takes a full-width
/// row underneath, so it never has to shrink or wrap.
///
/// The test is the sentence's own width, not a window breakpoint: a late-running sentence is longer
/// than the on-time one and needs the extra row sooner.
/// </summary>
public sealed partial class HeadlineLayoutPanel : Panel
{
    /// <summary>Gap between the text column and the car.</summary>
    public double ColumnSpacing { get; set; } = 12;

    /// <summary>Gap between the title and the sentence.</summary>
    public double RowSpacing { get; set; } = 4;

    private bool _stacked;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 3)
        {
            return base.MeasureOverride(availableSize);
        }

        UIElement title = Children[0], car = Children[1], line = Children[2];
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);

        car.Measure(unbounded);
        line.Measure(unbounded);

        var textWidth = Math.Max(0, availableSize.Width - car.DesiredSize.Width - ColumnSpacing);
        _stacked = line.DesiredSize.Width > textWidth;

        title.Measure(new Size(textWidth, double.PositiveInfinity));

        if (_stacked)
        {
            // Full width: the Viewbox inside still scales down if even that is not enough.
            line.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var top = Math.Max(title.DesiredSize.Height, car.DesiredSize.Height);
            return new Size(availableSize.Width, top + RowSpacing + line.DesiredSize.Height);
        }

        line.Measure(new Size(textWidth, double.PositiveInfinity));
        var text = title.DesiredSize.Height + RowSpacing + line.DesiredSize.Height;
        return new Size(availableSize.Width, Math.Max(text, car.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 3)
        {
            return base.ArrangeOverride(finalSize);
        }

        UIElement title = Children[0], car = Children[1], line = Children[2];
        var carWidth = car.DesiredSize.Width;
        var carX = finalSize.Width - carWidth;
        var textWidth = Math.Max(0, carX - ColumnSpacing);

        if (_stacked)
        {
            var top = Math.Max(title.DesiredSize.Height, car.DesiredSize.Height);
            title.Arrange(new Rect(0, (top - title.DesiredSize.Height) / 2, textWidth, title.DesiredSize.Height));
            car.Arrange(new Rect(carX, (top - car.DesiredSize.Height) / 2, carWidth, car.DesiredSize.Height));
            line.Arrange(new Rect(0, top + RowSpacing, finalSize.Width, line.DesiredSize.Height));
            return finalSize;
        }

        var textHeight = title.DesiredSize.Height + RowSpacing + line.DesiredSize.Height;
        var textY = (finalSize.Height - textHeight) / 2;
        title.Arrange(new Rect(0, textY, textWidth, title.DesiredSize.Height));
        line.Arrange(new Rect(0, textY + title.DesiredSize.Height + RowSpacing, textWidth, line.DesiredSize.Height));
        car.Arrange(new Rect(carX, (finalSize.Height - car.DesiredSize.Height) / 2, carWidth, car.DesiredSize.Height));
        return finalSize;
    }
}
