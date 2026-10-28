namespace Carte.Presentation;

public sealed partial class CapturePage : Page
{
    public CapturePage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            ViewModel = DataContext as CaptureViewModel;
            Bindings.Update();
        };
    }

    public CaptureViewModel? ViewModel { get; private set; }
}
