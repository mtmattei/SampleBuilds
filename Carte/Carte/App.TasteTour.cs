#if DEBUG
using Carte.Services;
using Microsoft.UI.Xaml.Media;

namespace Carte;

public partial class App
{
    // Opt-in recording harness. All changes use the same view-model commands as the UI.
    // MenuSession isolates these example preferences from the diner's persisted profile.
    private async Task StartTasteTourAsync()
    {
        MainWindow!.Activate();
        var session = Host!.Services.GetRequiredService<MenuSession>();
        await session.EnsureLoadedAsync();
        var step = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try
            {
                Console.WriteLine($"Carte taste tour: step {step} at {DateTime.UtcNow:O}");
                switch (step++)
                {
                    case 0:
                        await FindTourPage<ExplorePage>()!.ViewModel!.ShowTasteCommand.ExecuteAsync(null);
                        var form = FindTourPage<TastePage>()!.ViewModel!;
                        form.Light = true;
                        form.Bold = true;
                        form.SomeHeat = true;
                        form.LovesZesty = true;
                        form.AvoidText = "mushrooms";
                        timer.Interval = TimeSpan.FromSeconds(4);
                        break;
                    case 1:
                        await FindTourPage<TastePage>()!.ViewModel!.FindDishesCommand.ExecuteAsync(null);
                        timer.Interval = TimeSpan.FromSeconds(6);
                        break;
                    case 2:
                        var taste = FindTourPage<TastePage>()!.ViewModel!;
                        await taste.Recommendations[0].ShowMapCommand.ExecuteAsync(null);
                        timer.Interval = TimeSpan.FromSeconds(6);
                        break;
                    case 3:
                        session.SelectCategory("wine");
                        session.SetView(ExploreView.Menu);
                        timer.Interval = TimeSpan.FromSeconds(4);
                        break;
                    case 4:
                        session.SetView(ExploreView.Map);
                        timer.Interval = TimeSpan.FromSeconds(5);
                        break;
                    case 5:
                        session.SelectCategory("cocktails");
                        session.SetView(ExploreView.Menu);
                        timer.Interval = TimeSpan.FromSeconds(4);
                        break;
                    case 6:
                        session.SetView(ExploreView.Map);
                        timer.Interval = TimeSpan.FromSeconds(5);
                        break;
                    default:
                        Console.WriteLine("Carte taste tour: complete.");
                        return;
                }
                timer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Carte taste tour stopped: {ex}");
            }
        };
        timer.Start();
    }

    private T? FindTourPage<T>() where T : class, DependencyObject
    {
        T? Find(DependencyObject root)
        {
            if (root is T match) return match;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (Find(VisualTreeHelper.GetChild(root, i)) is { } child) return child;
            return null;
        }
        return MainWindow?.Content is { } content ? Find(content) : null;
    }
}
#endif
