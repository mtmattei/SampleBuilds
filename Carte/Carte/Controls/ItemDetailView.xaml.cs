using Carte.Presentation;

namespace Carte.Controls;

// Item description, "Why here?" evidence, save and pair. Used in the detail panel and inside an expanded menu row.
public sealed partial class ItemDetailView : UserControl
{
    public ItemDetailView() => InitializeComponent();

    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(MenuItemViewModel), typeof(ItemDetailView),
        new PropertyMetadata(null, (d, _) => ((ItemDetailView)d).Bindings.Update()));

    public static readonly DependencyProperty ShowHeaderProperty = DependencyProperty.Register(
        nameof(ShowHeader), typeof(bool), typeof(ItemDetailView),
        new PropertyMetadata(true, (d, _) => ((ItemDetailView)d).Bindings.Update()));

    public MenuItemViewModel? Item
    {
        get => (MenuItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public bool ShowHeader
    {
        get => (bool)GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }
}
