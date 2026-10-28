using Carte.Core;
using Carte.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace Carte.Presentation;

public sealed partial class ExploreViewModel : ObservableObject, IRecipient<SessionChangedMessage>
{
    private readonly INavigator _navigator;
    private readonly MenuSession _session;
    private IReadOnlyList<MenuItemViewModel> _allItems = [];
    private MenuItemViewModel? _announcedSelection;

    public ExploreViewModel(INavigator navigator, MenuSession session)
    {
        _navigator = navigator;
        _session = session;
        WeakReferenceMessenger.Default.Register(this);
        if (session.IsLoaded) Rebuild();
        _ = session.EnsureLoadedAsync();
    }

    public string MenuName => _session.Menu?.Name ?? "";
    public string MenuSubtitle => _session.Menu is { } menu
        ? Loc.Format("Format.MenuSubtitle", Loc.Get("Demo.MenuDescriptor"), menu.Items.Count)
        : Loc.Get("ReviewPage.Reading.Text");
    public bool IsIllustrative => _session.Menu?.IsIllustrative == true;
    public bool IsLoading => !_session.IsLoaded && _session.LoadError is null;
    public bool HasError => _session.LoadError is not null;

    [ObservableProperty] public partial IReadOnlyList<CategoryViewModel> Categories { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<MenuItemViewModel> VisibleItems { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<MenuItemViewModel> UnplacedItems { get; private set; } = [];
    [ObservableProperty] public partial AxisSet? AxisSet { get; private set; }
    [ObservableProperty] public partial string StatusMessage { get; private set; } = "";

    public bool HasUnplaced => UnplacedItems.Count > 0;

    // Two-way with the map and the list. Writing it selects through the session.
    public MenuItemViewModel? SelectedItem
    {
        get => _allItems.FirstOrDefault(i => i.IsSelected);
        set { if (value is not null) _session.Select(value.Id); }
    }

    public bool IsMapView => _session.View == ExploreView.Map;
    public Visibility MapVisibility => IsMapView ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ListVisibility => IsMapView ? Visibility.Collapsed : Visibility.Visible;
    public string ViewBarLabel => Loc.Get(IsMapView ? "ExplorePage.MapTitle.Text" : "ExplorePage.ListTitle.Text");
    public string OtherViewLabel => Loc.Get(IsMapView ? "ExplorePage.MenuView.Content" : "ExplorePage.MapView.Content");
    public ImageSource OtherViewGlyph => Glyphs.Ink(IsMapView ? "list" : "move");
    public string AxisSummary => AxisSet is { } a ? Loc.Format("Format.AxisSummary", a.X.Low, a.X.High, a.Y.Low, a.Y.High) : "";
    public string MapNote => Loc.Get(VisibleItems.Any(i => i.IsLowConfidence) ? "ExplorePage.UncertainLegend.Text" : "ExplorePage.RelativeNote.Text");
    public string TasteReason => _session.Recommendations.FirstOrDefault(r => r.Item.Id == SelectedItem?.Id)?.Reason ?? "";
    public bool ShowTasteReason => IsMapView && TasteReason.Length > 0;

    // At 905 and wider the detail lives in a side panel; below it, the menu row expands in place.
    [ObservableProperty] public partial bool IsWide { get; set; }
    public bool ShowDetailPanel => SelectedItem is not null && (IsMapView || IsWide);

    public bool HasPairing => _session.PairFrom is not null;
    public string PairNotice => _session.PairFrom is { } from && SelectedItem is { } to
        ? Loc.Format("Format.PairSuggestion", from.Name, to.Name) : "";
    public string PairNote => Loc.Get("Explore.PairNote");
    public string PairReturnLabel => _session.PairFrom is { } from ? Loc.Format("Format.PairReturn", from.Name) : "";

    public int TonightCount => _session.SavedIds.Count;
    public string TonightName => Loc.Format("Format.TonightName", TonightCount);

    partial void OnIsWideChanged(bool value) => RefreshItems(SessionChange.Selection);

    public void Receive(SessionChangedMessage message)
    {
        if (message.Change.HasFlag(SessionChange.Menu)) Rebuild();
        else RefreshItems(message.Change);
    }

    private void Rebuild()
    {
        if (_session.Menu is not { } menu)
        {
            RaiseAll();
            return;
        }
        _allItems = menu.Items
            .Select(i => new MenuItemViewModel(i, menu.Categories.First(c => c.Id == i.CategoryId), menu, _session))
            .ToArray();
        Categories = menu.Categories
            .Select(c => new CategoryViewModel(c, menu.Items.Count(i => i.CategoryId == c.Id), _session))
            .ToArray();
        RefreshItems(SessionChange.All);
    }

    private void RefreshItems(SessionChange change)
    {
        foreach (var item in _allItems) item.Refresh(inlineDetails: !IsWide && !IsMapView);
        if (change.HasFlag(SessionChange.Category) || change.HasFlag(SessionChange.Menu))
        {
            VisibleItems = _allItems.Where(i => i.Item.CategoryId == _session.CategoryId).ToArray();
            UnplacedItems = VisibleItems.Where(i => i.IsUnplaced).ToArray();
            AxisSet = _session.Category?.AxisSet;
            foreach (var category in Categories) category.Refresh();
        }
        if ((change & (SessionChange.Menu | SessionChange.Category | SessionChange.View)) != 0)
            BeginAppearance();
        Announce(change);
        RaiseAll();
    }

    public void BeginAppearance()
    {
        for (var index = 0; index < VisibleItems.Count; index++)
            VisibleItems[index].BeginAppearance(index);
    }

    private void Announce(SessionChange change)
    {
        var selected = SelectedItem;
        if (change.HasFlag(SessionChange.Shortlist) && selected is not null)
            StatusMessage = Loc.Format(selected.IsSaved ? "Format.SavedItem" : "Format.RemovedItem", selected.Name);
        else if (change.HasFlag(SessionChange.Selection) && selected is not null && selected != _announcedSelection)
            StatusMessage = Loc.Format("Format.SelectedItem", selected.Name);
        _announcedSelection = selected;
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(MenuName));
        OnPropertyChanged(nameof(MenuSubtitle));
        OnPropertyChanged(nameof(IsIllustrative));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(SelectedItem));
        OnPropertyChanged(nameof(HasUnplaced));
        OnPropertyChanged(nameof(IsMapView));
        OnPropertyChanged(nameof(MapVisibility));
        OnPropertyChanged(nameof(ListVisibility));
        OnPropertyChanged(nameof(ViewBarLabel));
        OnPropertyChanged(nameof(OtherViewLabel));
        OnPropertyChanged(nameof(OtherViewGlyph));
        OnPropertyChanged(nameof(AxisSummary));
        OnPropertyChanged(nameof(MapNote));
        OnPropertyChanged(nameof(TasteReason));
        OnPropertyChanged(nameof(ShowTasteReason));
        OnPropertyChanged(nameof(ShowDetailPanel));
        OnPropertyChanged(nameof(HasPairing));
        OnPropertyChanged(nameof(PairNotice));
        OnPropertyChanged(nameof(PairReturnLabel));
        OnPropertyChanged(nameof(TonightCount));
        OnPropertyChanged(nameof(TonightName));
    }

    [RelayCommand]
    private void ToggleView() => _session.SetView(IsMapView ? ExploreView.Menu : ExploreView.Map);

    [RelayCommand]
    private void ReturnFromPair() => _session.ReturnFromPair();

    [RelayCommand]
    private Task Retry() => _session.RetryAsync();

    [RelayCommand]
    private Task ScanMenu() => _navigator.NavigateRouteAsync(this, "Capture");

    [RelayCommand]
    private Task ShowTonight() => _navigator.NavigateRouteAsync(this, "Tonight");

    [RelayCommand]
    private Task ShowTaste() => _navigator.NavigateRouteAsync(this, "Taste");
}
