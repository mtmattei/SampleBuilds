using System.Diagnostics.CodeAnalysis;
using Uno.Resizetizer;

namespace MorningCard;

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
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);

                    // Uno Platform namespace filter groups
                    // Uncomment individual methods to see more detailed logging
                    //// Generic Xaml events
                    //logBuilder.XamlLogLevel(LogLevel.Debug);
                    //// Layout specific messages
                    //logBuilder.XamlLayoutLogLevel(LogLevel.Debug);
                    //// Storage messages
                    //logBuilder.StorageLogLevel(LogLevel.Debug);
                    //// Binding related messages
                    //logBuilder.XamlBindingLogLevel(LogLevel.Debug);
                    //// Binder memory references tracking
                    //logBuilder.BinderMemoryReferenceLogLevel(LogLevel.Debug);
                    //// DevServer and HotReload related
                    //logBuilder.HotReloadCoreLogLevel(LogLevel.Information);
                    //// Debug JS interop
                    //logBuilder.WebAssemblyLogLevel(LogLevel.Debug);

                }, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                )
                // Enable localization (see appsettings.json for supported languages)
                .UseLocalization()
                .ConfigureServices((context, services) =>
                {
                    // One hub stands in for the routing service and the connected car, so the two
                    // can never disagree about what state the app is in.
                    services
                        .AddSingleton(TimeProvider.System)
                        .AddSingleton<SimulatedDriveHub>()
                        .AddSingleton<ITripService>(sp => sp.GetRequiredService<SimulatedDriveHub>())
                        .AddSingleton<IVehicleService>(sp => sp.GetRequiredService<SimulatedDriveHub>());
                })
                .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes)
            );
        MainWindow = builder.Window;

#if DEBUG
        // UseStudio() never creates the window when no DevServer is reachable, so headless runs opt out.
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
        {
            MainWindow.UseStudio();
        }
#endif
        MainWindow.SetWindowIcon();

        var host = builder.Build();

#if DEBUG
        // Before navigation, so a seeded mode is in force when the feeds first subscribe. Applied
        // afterwards, MC_STATE=loading arrived too late to matter: every feed already held the
        // default Live state, so the loading capture was pixel-identical to the live one and the
        // progress templates were never exercised at all.
        var hub = host.Services.GetRequiredService<SimulatedDriveHub>();
        DebugHooks.ApplyTo(hub);
#endif

        Host = await MainWindow.InitializeNavigationAsync(
            () => Task.FromResult(host),
            initialRoute: "Main"
        );

#if DEBUG

        if (DebugHooks.ProbeRetry && MainWindow.Content is { } content)
        {
            _ = DebugHooks.ProbeRetryAsync(content, hub);
        }

        if (DebugHooks.ProbeFocus && MainWindow.Content is { } focusRoot)
        {
            _ = DebugHooks.ProbeFocusAsync(focusRoot);
        }

        if (DebugHooks.ProbeChips && MainWindow.Content is { } chipRoot)
        {
            _ = DebugHooks.ProbeChipsAsync(chipRoot);
        }

        if (DebugHooks.OpenTarget is { } openTarget && MainWindow.Content is { } openRoot)
        {
            _ = DebugHooks.OpenAsync(openRoot, openTarget);
        }
#endif
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap<MainPage, MainModel>(),
            new ViewMap<TuningPanel, TuningModel>()
        );

        routes.Register(
            // The Simulator is a nested region rather than a property on the card's model: a model
            // exposed as a property would hand XAML its raw IState objects with no bindable proxy.
            new RouteMap("Main", View: views.FindByViewModel<MainModel>(), IsDefault: true, Nested:
            [
                new RouteMap("Simulator", View: views.FindByViewModel<TuningModel>(), IsDefault: true)
            ])
        );
    }
}



