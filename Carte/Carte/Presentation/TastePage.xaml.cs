using System.ComponentModel;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Carte.Presentation;

public sealed partial class TastePage : Page
{
    public TastePage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }
    public TasteViewModel? ViewModel { get; private set; }
    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel is { } old) old.PropertyChanged -= OnPropertyChanged;
        ViewModel = DataContext as TasteViewModel;
        if (ViewModel is { } vm) vm.PropertyChanged += OnPropertyChanged;
        Bindings.Update();
    }
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(TasteViewModel.IsEditing)) PageScroll.ChangeView(null, 0, null, true);
        if (args.PropertyName == nameof(TasteViewModel.StatusMessage))
            FrameworkElementAutomationPeer.CreatePeerForElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
