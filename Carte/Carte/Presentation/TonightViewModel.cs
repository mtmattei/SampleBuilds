using Carte.Core;
using Carte.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace Carte.Presentation;

public sealed partial class TonightViewModel : ObservableObject, IRecipient<SessionChangedMessage>
{
    private readonly INavigator _navigator;
    private readonly MenuSession _session;

    public TonightViewModel(INavigator navigator, MenuSession session)
    {
        _navigator = navigator;
        _session = session;
        WeakReferenceMessenger.Default.Register(this);
        Rebuild();
        _ = session.EnsureLoadedAsync();
    }

    public string MenuName => _session.Menu?.Name ?? "";

    [ObservableProperty] public partial IReadOnlyList<MenuItemViewModel> Items { get; private set; } = [];
    [ObservableProperty] public partial string StatusMessage { get; private set; } = "";

    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => _session.IsLoaded && Items.Count == 0;

    // A planning subtotal of known menu prices, one of each. Never taxes, sizes or quantities.
    public string SubtotalText => _session.Subtotal is { } total && _session.Menu is { } menu
        ? PriceFormatter.FormatAmount(total.KnownAmount, total.Currency, menu.Locale) : "";
    public bool HasMissingPrices => _session.Subtotal?.MissingPriceCount > 0;

    public void Receive(SessionChangedMessage message)
    {
        if (message.Change.HasFlag(SessionChange.Menu) || message.Change.HasFlag(SessionChange.Shortlist)) Rebuild();
    }

    private void Rebuild()
    {
        var before = Items;
        if (_session.Menu is { } menu)
        {
            // Keep menu order so the shortlist reads like the menu.
            Items = menu.Items
                .Where(i => _session.IsSaved(i.Id))
                .Select(i => before.FirstOrDefault(b => b.Id == i.Id)
                    ?? new MenuItemViewModel(i, menu.Categories.First(c => c.Id == i.CategoryId), menu, _session))
                .ToArray();
        }
        if (before.FirstOrDefault(b => Items.All(i => i.Id != b.Id)) is { } removed)
            StatusMessage = Loc.Format("Format.RemovedItem", removed.Name);
        OnPropertyChanged(nameof(MenuName));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(HasMissingPrices));
    }

    // Opening a saved item returns to Explore with that item selected in its category.
    public async void OpenItem(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not MenuItemViewModel item) return;
        _session.Select(item.Id);
        await _navigator.ReturnToMenuAsync(this);
    }

    [RelayCommand]
    private Task KeepExploring() => _navigator.ReturnToMenuAsync(this);

    [RelayCommand]
    private Task ScanMenu() => _navigator.NavigateRouteAsync(this, "Capture");
}
