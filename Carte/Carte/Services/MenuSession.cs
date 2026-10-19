using Carte.Core;
using CommunityToolkit.Mvvm.Messaging;
using Windows.Storage;

namespace Carte.Services;

public enum ExploreView { Menu, Map }

[Flags]
public enum SessionChange
{
    None = 0,
    Menu = 1,
    Category = 2,
    Selection = 4,
    View = 8,
    Shortlist = 16,
    Pairing = 32,
    Rationale = 64,
    All = Menu | Category | Selection | View | Shortlist | Pairing | Rationale
}

public sealed record SessionChangedMessage(SessionChange Change);

// One menu, one shortlist, one selection. Menu margins and Taste atlas are two views of this state.
// Page view models listen through the weak messenger, so navigation can recreate them freely.
public sealed class MenuSession(ILogger<MenuSession> logger)
{
    private const string FixtureUri = "ms-appx:///Assets/Data/menu.sample.json";

    private readonly Dictionary<string, string> _selectedByCategory = new(StringComparer.Ordinal);
    private readonly TonightShortlist _shortlist = new();
    private Task? _loading;
    private IMenuStore? _store;

    public MenuDocument? Menu { get; private set; }
    public string? LoadError { get; private set; }
    public bool IsLoaded => Menu is not null;
    public string CategoryId { get; private set; } = "";
    public ExploreView View { get; private set; } = ExploreView.Menu;
    public MenuItem? PairFrom { get; private set; }
    public bool IsRationaleOpen { get; private set; }

    public MenuCategory? Category => Menu?.Categories.FirstOrDefault(c => c.Id == CategoryId);
    public MenuItem? SelectedItem =>
        Menu is not null && _selectedByCategory.TryGetValue(CategoryId, out var id) ? Find(id) : null;
    public IReadOnlyCollection<string> SavedIds => _shortlist.ItemIds;
    public bool IsSaved(string itemId) => _shortlist.ItemIds.Contains(itemId);
    public ShortlistSubtotal? Subtotal => Menu is null ? null : _shortlist.GetSubtotal(Menu);

    public MenuItem? Find(string itemId) => Menu?.Items.FirstOrDefault(i => i.Id == itemId);
    public MenuCategory? CategoryOf(MenuItem item) => Menu?.Categories.FirstOrDefault(c => c.Id == item.CategoryId);
    public IEnumerable<MenuItem> ItemsIn(string categoryId) =>
        Menu?.Items.Where(i => i.CategoryId == categoryId) ?? [];

    public Task EnsureLoadedAsync() => _loading ??= LoadAsync();

    public Task RetryAsync()
    {
        _loading = null;
        LoadError = null;
        Notify(SessionChange.Menu);
        return EnsureLoadedAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            // On WebAssembly this await also waits for the IndexedDB-backed file system.
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("carte", CreationCollisionOption.OpenIfExists);
            _store = new FileMenuStore(folder.Path);

            var fixture = await StorageFile.GetFileFromApplicationUriAsync(new Uri(FixtureUri));
            var packaged = MenuDocument.FromJson(await FileIO.ReadTextAsync(fixture));
            var menu = await _store.LoadMenuAsync(packaged.Id, CancellationToken.None);
            if (menu is null)
            {
                menu = packaged;
                await _store.SaveMenuAsync(menu, CancellationToken.None);
            }

            Menu = menu;
            if (Environment.GetEnvironmentVariable("CARTE_FRESH") != "1")
                _shortlist.Restore(await _store.LoadShortlistAsync(menu.Id, CancellationToken.None), menu);
            CategoryId = menu.Categories[0].Id;
            EnsureSelection();
            ApplyDebugStartState();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load the menu.");
            LoadError = ex.Message;
            _loading = null;
        }
        Notify(SessionChange.All);
    }

    // Retains the category's previous selection, otherwise selects its first item.
    public void SelectCategory(string categoryId)
    {
        if (Menu is null || categoryId == CategoryId || Menu.Categories.All(c => c.Id != categoryId)) return;
        CategoryId = categoryId;
        PairFrom = null;
        IsRationaleOpen = false;
        EnsureSelection();
        Notify(SessionChange.Category | SessionChange.Selection | SessionChange.Pairing | SessionChange.Rationale);
    }

    public void Select(string itemId)
    {
        if (Find(itemId) is not { } item) return;
        var categoryChanged = item.CategoryId != CategoryId;
        if (!categoryChanged && SelectedItem?.Id == itemId) return;
        // A pairing line names the suggested item, so choosing anything else retires it.
        var change = SessionChange.Selection | SessionChange.Rationale | (PairFrom is null ? 0 : SessionChange.Pairing);
        if (categoryChanged) change |= SessionChange.Category;
        CategoryId = item.CategoryId;
        PairFrom = null;
        _selectedByCategory[CategoryId] = itemId;
        IsRationaleOpen = false;
        Notify(change);
    }

    public void SetView(ExploreView view)
    {
        if (View == view) return;
        View = view;
        Notify(SessionChange.View);
    }

    public void ToggleRationale()
    {
        IsRationaleOpen = !IsRationaleOpen;
        Notify(SessionChange.Rationale);
    }

    public async Task<bool> ToggleSavedAsync(string itemId)
    {
        if (Menu is null) return false;
        var saved = _shortlist.Toggle(itemId, Menu);
        Notify(SessionChange.Shortlist);
        await PersistShortlistAsync();
        return saved;
    }

    // Pairings come from fixture links, never from distances across different axis sets.
    public MenuItem? PairingFor(MenuItem item) =>
        item.PairingItemIds.Select(Find).FirstOrDefault(p => p is not null && p.CategoryId != item.CategoryId);

    public void Pair(string fromItemId)
    {
        if (Find(fromItemId) is not { } from || PairingFor(from) is not { } target) return;
        CategoryId = target.CategoryId;
        _selectedByCategory[CategoryId] = target.Id;
        PairFrom = from;
        IsRationaleOpen = false;
        Notify(SessionChange.Category | SessionChange.Selection | SessionChange.Pairing | SessionChange.Rationale);
    }

    public void ReturnFromPair()
    {
        if (PairFrom is not { } from) return;
        PairFrom = null;
        CategoryId = from.CategoryId;
        _selectedByCategory[CategoryId] = from.Id;
        IsRationaleOpen = false;
        Notify(SessionChange.Category | SessionChange.Selection | SessionChange.Pairing | SessionChange.Rationale);
    }

    // The demo review confirms the same reviewed menu; a real capture would replace Menu here.
    public async Task ConfirmReviewedMenuAsync()
    {
        if (Menu is null || _store is null) return;
        await _store.SaveMenuAsync(Menu, CancellationToken.None);
    }

    private void EnsureSelection()
    {
        if (Menu is null || _selectedByCategory.ContainsKey(CategoryId)) return;
        if (ItemsIn(CategoryId).FirstOrDefault() is { } first) _selectedByCategory[CategoryId] = first.Id;
    }

    private async Task PersistShortlistAsync()
    {
        if (Menu is null || _store is null) return;
        try
        {
            await _store.SaveShortlistAsync(Menu.Id, _shortlist.ItemIds, CancellationToken.None);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not save the shortlist.");
        }
    }

    // DEBUG-only start state so runtime captures are deterministic without a pointer.
    [System.Diagnostics.Conditional("DEBUG")]
    private void ApplyDebugStartState()
    {
        string? Env(string name) => Environment.GetEnvironmentVariable(name);
        if (Env("CARTE_SAVE") is { } saved)
            _shortlist.Restore(saved.Split(',', StringSplitOptions.RemoveEmptyEntries), Menu!);
        if (Env("CARTE_VIEW") == "map") View = ExploreView.Map;
        if (Env("CARTE_CATEGORY") is { } category) SelectCategory(category);
        if (Env("CARTE_SELECT") is { } item) Select(item);
        if (Env("CARTE_PAIR") is { } from) Pair(from);
        if (Env("CARTE_WHY") == "1") IsRationaleOpen = true;
    }

    private static void Notify(SessionChange change) =>
        WeakReferenceMessenger.Default.Send(new SessionChangedMessage(change));
}
