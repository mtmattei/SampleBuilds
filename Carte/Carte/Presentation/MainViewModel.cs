namespace Carte.Presentation;

public partial class MainViewModel : ObservableObject
{

    public MainViewModel(
        INavigator navigator)
    {
        Title = "Main";
    }
    public string? Title { get; }


}
