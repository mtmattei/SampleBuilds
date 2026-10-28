using System.Diagnostics.CodeAnalysis;
using Carte.Services;
using Uno.Resizetizer;

namespace Carte;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)
                        .CoreLogLevel(LogLevel.Warning);
                }, enableUnoLogging: true)
                .ConfigureServices((context, services) =>
                {
                    // One shared menu session: both Explore views and Tonight read the same state.
                    services.AddSingleton<MenuSession>();
                })
                .UseNavigation(RegisterRoutes)
            );
        MainWindow = builder.Window;

#if DEBUG
        // A debug-only portrait window for capturing the real responsive app.
        if (Environment.GetCommandLineArgs().Contains("--portrait-preview"))
        {
            Environment.SetEnvironmentVariable("CARTE_WIDTH", "453");
            Environment.SetEnvironmentVariable("CARTE_HEIGHT", "1000");
            Environment.SetEnvironmentVariable("APP_NO_HOTDESIGN", "1");
        }
        // Hot Design needs a reachable DevServer; headless verification runs opt out.
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
            MainWindow.UseStudio();
        if (int.TryParse(Environment.GetEnvironmentVariable("CARTE_WIDTH"), out var width)
            && int.TryParse(Environment.GetEnvironmentVariable("CARTE_HEIGHT"), out var height))
            MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });
        if (Environment.GetCommandLineArgs().Contains("--portrait-preview"))
            MainWindow.AppWindow.Move(new Windows.Graphics.PointInt32 { X = 24, Y = 12 });
        var initialRoute = Environment.GetEnvironmentVariable("CARTE_ROUTE") ?? "Explore";
#else
        const string initialRoute = "Explore";
#endif
        MainWindow.SetWindowIcon();

        Host = await MainWindow.InitializeNavigationAsync(
            () => Task.FromResult(builder.Build()),
            initialRoute: initialRoute
        );
#if DEBUG
        if (Environment.GetCommandLineArgs().Contains("--taste-tour"))
            await StartTasteTourAsync();
        // Reproducible native footage: use the real category/session behavior.
        // This is opt-in and never runs in ordinary app launches or release builds.
        if (Environment.GetCommandLineArgs().Contains("--recording-tour"))
        {
            MainWindow.Activate();
            var session = Host.Services.GetRequiredService<MenuSession>();
            await session.EnsureLoadedAsync();
            var categories = session.Menu?.Categories.Select(category => category.Id).ToArray() ?? [];
            var step = 1;
            var tour = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            tour.Tick += (_, _) =>
            {
                if (step < categories.Length) session.SelectCategory(categories[step++]);
                else tour.Stop();
            };
            tour.Start();
        }
#endif
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap<ExplorePage, ExploreViewModel>(),
            new ViewMap<TastePage, TasteViewModel>(),
            new ViewMap<TonightPage, TonightViewModel>(),
            new ViewMap<CapturePage, CaptureViewModel>(),
            new ViewMap<ReviewPage, ReviewViewModel>()
        );

        routes.Register(
            new RouteMap("Explore", View: views.FindByViewModel<ExploreViewModel>(), IsDefault: true),
            new RouteMap("Taste", View: views.FindByViewModel<TasteViewModel>()),
            new RouteMap("Tonight", View: views.FindByViewModel<TonightViewModel>()),
            new RouteMap("Capture", View: views.FindByViewModel<CaptureViewModel>()),
            new RouteMap("Review", View: views.FindByViewModel<ReviewViewModel>())
        );
    }
}
