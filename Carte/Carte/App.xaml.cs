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
        // Hot Design needs a reachable DevServer; headless verification runs opt out.
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
            MainWindow.UseStudio();
        if (int.TryParse(Environment.GetEnvironmentVariable("CARTE_WIDTH"), out var width)
            && int.TryParse(Environment.GetEnvironmentVariable("CARTE_HEIGHT"), out var height))
            MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });
        var initialRoute = Environment.GetEnvironmentVariable("CARTE_ROUTE") ?? "Explore";
#else
        const string initialRoute = "Explore";
#endif
        MainWindow.SetWindowIcon();

        Host = await MainWindow.InitializeNavigationAsync(
            () => Task.FromResult(builder.Build()),
            initialRoute: initialRoute
        );
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap<ExplorePage, ExploreViewModel>()
        );

        routes.Register(
            new RouteMap("Explore", View: views.FindByViewModel<ExploreViewModel>(), IsDefault: true)
        );
    }
}
