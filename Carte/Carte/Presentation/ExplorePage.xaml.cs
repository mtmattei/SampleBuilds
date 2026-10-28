using System.ComponentModel;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Carte.Presentation;

public sealed partial class ExplorePage : Page
{
    private const double WideWidth = 905;

    public ExplorePage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => ViewModel?.BeginAppearance();
        SizeChanged += (_, e) => { if (ViewModel is { } vm) vm.IsWide = e.NewSize.Width >= WideWidth; };
    }

    public ExploreViewModel? ViewModel { get; private set; }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel is { } old) old.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel = DataContext as ExploreViewModel;
        if (ViewModel is { } vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.IsWide = ActualWidth >= WideWidth;
        }
        Bindings.Update();
    }

    // Announce through a polite live region; the text itself is set by binding.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExploreViewModel.StatusMessage))
            FrameworkElementAutomationPeer.CreatePeerForElement(StatusText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
