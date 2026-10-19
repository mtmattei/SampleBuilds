namespace Carte.Presentation;

public sealed partial class ReviewPage : Page
{
    public ReviewPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            ViewModel = DataContext as ReviewViewModel;
            Bindings.Update();
        };
    }

    public ReviewViewModel? ViewModel { get; private set; }
}
