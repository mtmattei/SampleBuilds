using Carte.Core;
using Carte.Services;

namespace Carte.Presentation;

public sealed record CaptureLine(string Name, string Price);

// Illustrative capture: no camera, photo or upload. It previews the sample menu and simulates reading it.
public sealed partial class CaptureViewModel : ObservableObject
{
    private static readonly TimeSpan SimulatedRead = TimeSpan.FromSeconds(1.2);

    private readonly INavigator _navigator;
    private readonly MenuSession _session;
    private CancellationTokenSource? _reading;

    public CaptureViewModel(INavigator navigator, MenuSession session)
    {
        _navigator = navigator;
        _session = session;
        _ = LoadAsync();
    }

    public string MenuName => _session.Menu?.Name ?? "";
    public IReadOnlyList<CaptureLine> Lines => _session.Menu is { } menu
        ? menu.Items.Take(6).Select(i => new CaptureLine(i.Name,
            i.Price is { } p ? PriceFormatter.FormatAmount(p.Amount, p.Currency, menu.Locale) : "")).ToArray()
        : [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsReading { get; private set; }

    public bool IsIdle => !IsReading;

    private async Task LoadAsync()
    {
        await _session.EnsureLoadedAsync();
        OnPropertyChanged(nameof(MenuName));
        OnPropertyChanged(nameof(Lines));
    }

    [RelayCommand]
    private async Task Read()
    {
        _reading = new CancellationTokenSource();
        IsReading = true;
        try
        {
            await Task.Delay(SimulatedRead, _reading.Token);
            // A cancelled read never navigates, even if its delay was already finishing.
            if (!_reading.IsCancellationRequested) await _navigator.NavigateRouteAsync(this, "Review");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsReading = false;
        }
    }

    [RelayCommand]
    private void CancelRead() => _reading?.Cancel();

    [RelayCommand]
    private Task Back()
    {
        _reading?.Cancel();
        return _navigator.ReturnToMenuAsync(this);
    }
}

public sealed partial class ReviewViewModel : ObservableObject
{
    private readonly INavigator _navigator;
    private readonly MenuSession _session;

    public ReviewViewModel(INavigator navigator, MenuSession session)
    {
        _navigator = navigator;
        _session = session;
        _ = LoadAsync();
    }

    public string MenuName => _session.Menu?.Name ?? "";
    public string Summary => _session.Menu is { } menu
        ? Loc.Format("Format.CategoryCount", menu.Items.Count, menu.Categories.Count) : "";
    public IReadOnlyList<CategoryViewModel> Categories => _session.Menu is { } menu
        ? menu.Categories.Select(c => new CategoryViewModel(c, menu.Items.Count(i => i.CategoryId == c.Id), _session)).ToArray()
        : [];

    private async Task LoadAsync()
    {
        await _session.EnsureLoadedAsync();
        OnPropertyChanged(nameof(MenuName));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Categories));
    }

    // Review leads to Explore only after confirmation; capture screens leave the back stack.
    [RelayCommand]
    private async Task Confirm()
    {
        await _session.ConfirmReviewedMenuAsync();
        await _navigator.NavigateRouteAsync(this, "Explore", qualifier: Qualifiers.ClearBackStack);
    }

    [RelayCommand]
    private Task Back() => _navigator.NavigateBackAsync(this);
}
