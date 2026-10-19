using Carte.Core;
using Carte.Services;
using Microsoft.UI.Xaml.Media;

namespace Carte.Presentation;

// Presentation model for one menu item, shared by the map, the menu list, the detail panel and Tonight.
public sealed partial class MenuItemViewModel : ObservableObject
{
    private const double MiniMapSize = 44;
    private const double MiniDotSize = 7;

    private readonly MenuSession _session;

    public MenuItemViewModel(MenuItem item, MenuCategory category, MenuDocument menu, MenuSession session)
    {
        _session = session;
        Item = item;
        PriceText = item.Price is { } price ? PriceFormatter.Format(price, menu.Locale) : Loc.Get("ReviewPage.PriceUnknown.Text");
        SensorySummary = string.Join(" · ", item.SensoryTags);
        TasteWords = item.Placement is { } p ? TasteDescriber.Describe(p, category.AxisSet) : Loc.Get("ExplorePage.Unplaced.Text");
        var confidence = IsLowConfidence ? Loc.Get("Explore.LowConfidence") : Loc.Get("Explore.Estimated");
        AccessibleName = Loc.Format("Format.ItemAccessibleName", item.Name, TasteWords, $"{confidence}, {PriceText}");
        if (item.Placement is { } placed)
        {
            var centre = TasteMapGeometry.ToPixel(placed.X, placed.Y, MiniMapSize, MiniMapSize, MiniDotSize);
            MiniDotMargin = new Thickness(centre.X - MiniDotSize / 2, centre.Y - MiniDotSize / 2, 0, 0);
        }
    }

    public MenuItem Item { get; }
    public string Id => Item.Id;
    public string Name => Item.Name;
    public string ShortName => Item.ShortName;
    public string Description => Item.Description;
    public string SensorySummary { get; }
    public string PriceText { get; }
    public string SummaryLine => $"{SensorySummary} · {PriceText}";
    public string SourceLine => $"{Loc.Get("ItemDetail.SourcePrefix.Text")} “{Item.OriginalMenuText}”";
    public string Rationale => Item.Placement?.Rationale ?? Loc.Get("ExplorePage.Unplaced.Text");
    public string TasteWords { get; }
    public string AccessibleName { get; }
    public string RemoveName => Loc.Format("Format.RemoveItem", Item.Name);

    public bool IsPlaced => Item.Placement is not null;
    public bool IsUnplaced => !IsPlaced;
    public bool IsLowConfidence => Item.Placement?.Confidence == "low";
    public double X => Item.Placement?.X ?? 0;
    public double Y => Item.Placement?.Y ?? 0;
    public Thickness MiniDotMargin { get; }
    public bool ShowSolidDot => IsPlaced && !IsLowConfidence;
    public bool ShowRingDot => IsPlaced && IsLowConfidence;

    public ImageSource GlyphInk => Glyphs.Ink(Item.Glyph);
    public ImageSource GlyphPaper => Glyphs.Paper(Item.Glyph);

    // Session-derived state, refreshed by the owning page view model.
    [ObservableProperty] public partial bool IsSelected { get; private set; }
    [ObservableProperty] public partial bool IsSaved { get; private set; }
    [ObservableProperty] public partial bool ShowInlineDetail { get; private set; }
    [ObservableProperty] public partial bool ShowSelectionWash { get; private set; }
    [ObservableProperty] public partial bool IsRationaleOpen { get; private set; }

    public string SaveLabel => Loc.Get(IsSaved ? "ItemDetail.Saved.Content" : "ItemDetail.Save.Content");
    public ImageSource SaveGlyph => Glyphs.Paper(IsSaved ? "check" : "plus");
    public string WhyLabel => Loc.Get(IsRationaleOpen ? "ItemDetail.HideWhy.Content" : "ItemDetail.Why.Content");

    private MenuItem? PairTarget => _session.PairingFor(Item);
    public bool CanPair => PairTarget is not null;
    public string PairName => PairTarget is { } t ? Loc.Format("Format.PairItem", t.Name) : "";
    public ImageSource? PairGlyph => PairTarget is { } t && _session.CategoryOf(t) is { } c ? Glyphs.Ink(c.Glyph) : null;

    internal void Refresh(bool inlineDetails)
    {
        IsSelected = _session.SelectedItem?.Id == Id && _session.CategoryId == Item.CategoryId;
        IsSaved = _session.IsSaved(Id);
        ShowInlineDetail = IsSelected && inlineDetails;
        // When the detail lives in the side panel, the selected row still needs a visible mark.
        ShowSelectionWash = IsSelected && !inlineDetails;
        IsRationaleOpen = IsSelected && _session.IsRationaleOpen;
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(SaveGlyph));
        OnPropertyChanged(nameof(WhyLabel));
    }

    [RelayCommand]
    private void Select() => _session.Select(Id);

    [RelayCommand]
    private Task ToggleSaved() => _session.ToggleSavedAsync(Id);

    [RelayCommand]
    private void ToggleRationale()
    {
        _session.Select(Id);
        _session.ToggleRationale();
    }

    [RelayCommand]
    private void Pair() => _session.Pair(Id);

    // ListViewItem automation peers fall back to the item's string.
    public override string ToString() => AccessibleName;
}

public sealed partial class CategoryViewModel(MenuCategory category, int itemCount, MenuSession session) : ObservableObject
{
    public string Id => category.Id;
    public string Label => category.Label;
    public AxisSet AxisSet => category.AxisSet;
    public ImageSource Glyph => Glyphs.Ink(category.Glyph);
    public string CountText => Loc.Format("Format.ItemsCount", itemCount);

    // Two-way with the tab's IsChecked: checking a tab selects its category.
    public bool? IsSelected
    {
        get => session.CategoryId == Id;
        set { if (value == true) session.SelectCategory(Id); }
    }

    internal void Refresh() => OnPropertyChanged(nameof(IsSelected));
}
