namespace MorningCard.Controls;

/// <summary>
/// The card. It inherits the page's DataContext (the generated MainViewModel) and has no model of
/// its own: there is one card, and splitting its state from the page would only add a hop.
/// </summary>
public sealed partial class MorningCardView : UserControl
{
    public MorningCardView() => InitializeComponent();
}
