using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using AndroidX.Car.App.Validation;
using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Services;
using MorningCard.Presentation;
using CarAction = AndroidX.Car.App.Model.Action;

namespace MorningCard.Droid.Car;

/// <summary>
/// Android Auto: the morning's drop-off as a point-of-interest template (Uno AndroidAuto opt-in,
/// AndroidX Car App Library). The car cannot host XAML, so the host draws the map, the school
/// marker and the rows; this supplies the data from the same simulated hub the phone card reads.
/// </summary>
[Service(Exported = true, Label = "MorningCard")]
[IntentFilter(new[] { "androidx.car.app.CarAppService" }, Categories = new[] { "androidx.car.app.category.POI" })]
public class DropoffCarAppService : CarAppService
{
    // Any host, as the Car App Library samples do for development. A store build narrows this to
    // the Android Auto host's signature (HostValidator.Builder with the allowed hosts resource).
    public override HostValidator CreateHostValidator() => HostValidator.AllowAllHostsValidator;

    public override Session OnCreateSession() => new DropoffSession();
}

internal sealed class DropoffSession : Session
{
    public override Screen OnCreateScreen(Intent intent) => new DropoffScreen(CarContext);
}

/// <summary>
/// One screen, rebuilt whenever the hub changes: the headline row carries the school as its place
/// (the map marker), then the drive, the cabin and the parking; Start drive hands off to navigation.
/// </summary>
internal sealed class DropoffScreen : Screen
{
    private readonly Handler _main = new(Looper.MainLooper!);
    private readonly CancellationTokenSource _watch = new();

    public DropoffScreen(CarContext carContext)
        : base(carContext)
    {
        var hub = SharedHub.Instance;
        _ = LoadRouteAsync();
        _ = WatchAsync(hub.WatchLastKnown(_watch.Token));
        _ = WatchAsync(((IVehicleService)hub).Watch(_watch.Token));
        _ = WatchAsync(hub.WatchClock(_watch.Token));
    }

    private MorningCard.Core.Route.RouteGeometry? _route;
    private bool _routeLoaded;

    private async Task LoadRouteAsync()
    {
        // Straight from the APK's assets: the car session can run without the Uno app ever
        // starting, and ms-appx:/// (RouteGeometryLoader) only resolves once it has.
        try
        {
            using var stream = CarContext.Assets!.Open("Assets/route.json");
            using var reader = new StreamReader(stream);
            _route = MorningCard.Core.Route.RouteAsset.TryParse(await reader.ReadToEndAsync());
        }
        catch (Java.IO.IOException)
        {
            _route = null;
        }
        _routeLoaded = true;
        _main.Post(Invalidate);
    }

    private async Task WatchAsync<T>(IAsyncEnumerable<T> changes)
    {
        try
        {
            await foreach (var _ in changes)
            {
                // Screen.Invalidate belongs on the main thread; the host then asks for a new template.
                _main.Post(Invalidate);
            }
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    public override ITemplate OnGetTemplate()
    {
        var state = SharedHub.Instance.State;
        var template = new PlaceListMapTemplate.Builder()
            .SetTitle(state.Mode == ConnectionMode.NoTrip ? "This morning" : state.Trip.Title)
            .SetHeaderAction(CarAction.AppIcon);

        // The host shows its own spinner until the route (and so the distances) has loaded.
        if (!_routeLoaded)
        {
            return template.SetLoading(true).Build();
        }

        // Every row needs a distance, and the distances come from the route asset.
        if (_route is not { } route)
        {
            return template.SetItemList(new ItemList.Builder().SetNoItemsMessage("Route unavailable. Open Maps to navigate.").Build())
                .SetActionStrip(new ActionStrip.Builder().AddAction(StartDriveAction()).Build())
                .Build();
        }

        var items = new ItemList.Builder();

        if (state.Mode == ConnectionMode.NoTrip)
        {
            items.SetNoItemsMessage("No drop-off today. School is closed.");
        }
        else
        {
            var plan = state.Trip;
            var timeline = TripMath.Derive(plan, TripMath.FromMinutes(state.ClockMinutes));
            var headline = timeline.Tone switch
            {
                CardTone.Late => $"Running {Math.Max(0, TripMath.ToMinutes(timeline.Arrive) + plan.WalkMin - TripMath.ToMinutes(plan.Bell))} min late",
                CardTone.LeaveNow => "Leave now",
                _ => $"Leave in {timeline.SlackMin} min",
            };
            var traffic = timeline.Traffic switch
            {
                TrafficLevel.Heavy => "Heavy traffic",
                TrafficLevel.Moderate => "Moderate traffic",
                _ => "Light traffic",
            };
            var car = state.Vehicle;
            var cabin = car.IsStale ? "car offline" : car.ClimateOn ? "cabin warming" : $"{car.OutsideTemp}° outside";

            // PlaceListMapTemplate: every row that is not browsable carries a distance, and each
            // row here is a real place on the map - the school at the end of the walk, and the
            // parking spot at the end of the drive.
            items.AddItem(new Row.Builder()
                .SetTitle(headline)
                .AddText(WithDistance(route.Drive.TotalMeters + route.Walk.TotalMeters, $" · Drive {timeline.DriveMin} min · {traffic}"))
                .AddText($"Leave by {Clock(timeline.LeaveBy)} · arrive {Clock(timeline.Arrive)} · bell {Clock(plan.Bell)} · {cabin}")
                .SetMetadata(new Metadata.Builder().SetPlace(Place(Destination.Latitude, Destination.Longitude, SchoolMarker, null)).Build())
                .Build());

            var (parkLon, parkLat) = Mapsui.Projections.SphericalMercator.ToLonLat(route.Parking.X, route.Parking.Y);
            items.AddItem(new Row.Builder()
                .SetTitle(plan.ParkingStreet)
                .AddText(WithDistance(route.Drive.TotalMeters, $" · {plan.ParkingNote}"))
                .SetMetadata(new Metadata.Builder().SetPlace(Place(parkLat, parkLon, ParkingMarker, "P")).Build())
                .Build());
        }

        // The card's cabin Start (v6 Auto artboard) as the strip's second action, while the driver
        // has not decided and the car can act on it.
        var strip = new ActionStrip.Builder().AddAction(StartDriveAction());
        if (state.Mode != ConnectionMode.NoTrip && !state.Vehicle.IsStale && !state.Vehicle.ClimateOn)
        {
            strip.AddAction(new CarAction.Builder()
                .SetTitle("Warm cabin")
                .SetOnClickListener(new ClickListener(() => _ = SharedHub.Instance.SetClimateAsync(true, CancellationToken.None)))
                .Build());
        }

        return template
            .SetItemList(items.Build())
            .SetActionStrip(strip.Build())
            .SetAnchor(Place(Destination.Latitude, Destination.Longitude, SchoolMarker, null))
            .Build();
    }

    private CarAction StartDriveAction() => new CarAction.Builder()
        .SetTitle("Start drive")
        .SetOnClickListener(new ClickListener(StartNavigation))
        .Build();

    // The card's pin ink and parking green (PinColor #3A3C3F, GoodInkColor #4F6B4C in CardTokens).
    // The car session cannot read XAML resources - the Uno app may never have started - so the two
    // values are repeated here; the host picks the dark variant at night.
    private static readonly CarColor SchoolMarker = CarColor.CreateCustom(unchecked((int)0xFF3A3C3F), unchecked((int)0xFFE9E6DF));
    private static readonly CarColor ParkingMarker = CarColor.CreateCustom(unchecked((int)0xFF4F6B4C), unchecked((int)0xFFA9C2A5));

    private static Place Place(double latitude, double longitude, CarColor color, string? label)
    {
        var marker = new PlaceMarker.Builder().SetColor(color);
        if (label is not null)
        {
            marker.SetLabel(label);
        }

        return new Place.Builder(CarLocation.Create(latitude, longitude))
            .SetMarker(marker.Build())
            .Build();
    }

    /// <summary>The host formats the distance (units, locale) from a DistanceSpan on the first character.</summary>
    private static Android.Text.SpannableString WithDistance(double meters, string rest)
    {
        var text = new Android.Text.SpannableString(" " + rest);
        var distance = Distance.Create(Math.Round(meters / 1000d, 1), Distance.UnitKilometers);
        text.SetSpan(DistanceSpan.Create(distance), 0, 1, Android.Text.SpanTypes.InclusiveInclusive);
        return text;
    }

    /// <summary>Hands the drop-off to the car's navigation app (Car App Library ACTION_NAVIGATE).</summary>
    private void StartNavigation()
    {
        var geo = Android.Net.Uri.Parse(FormattableString.Invariant($"geo:{Destination.Latitude},{Destination.Longitude}"));
        CarContext.StartCarApp(new Intent(CarContext.ActionNavigate, geo));
    }

    private static string Clock(TimeOnly time) => time.ToString("H:mm", System.Globalization.CultureInfo.InvariantCulture);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watch.Cancel();
            _watch.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class ClickListener(System.Action onClick) : Java.Lang.Object, IOnClickListener
    {
        public void OnClick() => onClick();
    }
}
