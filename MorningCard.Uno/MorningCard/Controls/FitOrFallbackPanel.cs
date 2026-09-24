using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>
/// Shows its first child while that child fits on one line in the width it is given, and its second
/// child once it would not. The ruler's Walk block uses it to trade the word for a walking icon
/// before the word can wrap or clip.
///
/// A layout decision, so it lives in a panel rather than in a SizeChanged handler: it is re-made on
/// every measure, whether the width changed because the window did or because the walk got shorter.
/// {utu:Responsive} cannot do this, because it keys off the window, not off this slot.
/// </summary>
public sealed partial class FitOrFallbackPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }

        // Unconstrained width, so the preferred child reports its one-line width.
        var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
        Children[0].Measure(unbounded);
        Children[1].Measure(unbounded);

        var chosen = Fits(availableSize.Width) ? Children[0] : Children[1];
        return new Size(Math.Min(chosen.DesiredSize.Width, availableSize.Width), chosen.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }

        var fits = Fits(finalSize.Width);
        var full = new Rect(0, 0, finalSize.Width, finalSize.Height);

        // The unused child is arranged to an empty rect, which clips it away without touching its
        // Visibility, so nothing here writes to a property the XAML owns.
        Children[0].Arrange(fits ? full : default);
        Children[1].Arrange(fits ? default : full);
        return finalSize;
    }

    private bool Fits(double width) => Children[0].DesiredSize.Width <= width;
}
