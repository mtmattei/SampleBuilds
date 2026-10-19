using System.ComponentModel;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Carte.Presentation;

public sealed partial class TonightPage : Page
{
    public TonightPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public TonightViewModel? ViewModel { get; private set; }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel is { } old) old.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel = DataContext as TonightViewModel;
        if (ViewModel is { } vm) vm.PropertyChanged += OnViewModelPropertyChanged;
        Bindings.Update();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TonightViewModel.StatusMessage))
            FrameworkElementAutomationPeer.CreatePeerForElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
