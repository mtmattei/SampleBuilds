using System.Windows.Input;

namespace Carte.Controls;

// Wordmark and "Scan menu". The scan action is hidden while the capture flow is already open.
public sealed partial class AppHeader : UserControl
{
    public AppHeader()
    {
        InitializeComponent();
        ScanButton.Visibility = Visibility.Collapsed;
    }

    public static readonly DependencyProperty ScanCommandProperty = DependencyProperty.Register(
        nameof(ScanCommand), typeof(ICommand), typeof(AppHeader),
        new PropertyMetadata(null, (d, e) =>
            ((AppHeader)d).ScanButton.Visibility = e.NewValue is null ? Visibility.Collapsed : Visibility.Visible));

    public ICommand? ScanCommand
    {
        get => (ICommand?)GetValue(ScanCommandProperty);
        set => SetValue(ScanCommandProperty, value);
    }
}
