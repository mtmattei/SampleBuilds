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
        _ = WatchAsync(hub.WatchLastKnown(_watch.Token));
        _ = WatchAsync(((IVehicleService)hub).Watch(_watch.Token));
        _ = WatchAsync(hub.WatchClock(_watch.Token));
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
        var school = new Place.Builder(CarLocation.Create(Destination.Latitude, Destination.Longitude))
            .SetMarker(new PlaceMarker.Builder().Build())
            .Build();

        var items = new ItemList.Builder();

        if (state.Mode == ConnectionMode.NoTrip)
        {
            items.AddItem(new Row.Builder().SetTitle("No drop-off today").AddText("School is closed.").Build());
        }
        else
        {
            var plan = state.Trip;
            var now = TripMath.FromMinutes(state.ClockMinutes);
            var timeline = TripMath.Derive(plan, now);
            var headline = timeline.Tone switch
            {
                CardTone.Late => $"Running {Math.Max(0, TripMath.ToMinutes(timeline.Arrive) + plan.WalkMin - TripMath.ToMinutes(plan.Bell))} min late",
                CardTone.LeaveNow => "Leave now",
                _ => $"Leave in {timeline.SlackMin} min",
            };

            items.AddItem(new Row.Builder()
                .SetTitle(headline)
                .AddText($"Leave by {Clock(timeline.LeaveBy)} · arrive {Clock(timeline.Arrive)} · bell {Clock(plan.Bell)}")
                .SetMetadata(new Metadata.Builder().SetPlace(school).Build())
                .Build());

            items.AddItem(new Row.Builder()
                .SetTitle($"Drive {timeline.DriveMin} min")
                .AddText(timeline.Traffic switch
                {
                    TrafficLevel.Heavy => "Heavy traffic",
                    TrafficLevel.Moderate => "Moderate traffic",
                    _ => "Light traffic",
                })
                .Build());

            var car = state.Vehicle;
            items.AddItem(new Row.Builder()
                .SetTitle(car.IsStale ? "Car offline" : car.ClimateOn ? "Cabin warming" : "Warm the cabin")
                .AddText(car.IsStale ? $"Last seen {Clock(car.LastSeen)}" : $"Outside is {car.OutsideTemp}°")
                .Build());

            items.AddItem(new Row.Builder()
                .SetTitle(plan.ParkingStreet)
                .AddText(plan.ParkingNote)
                .Build());
        }

        var startDrive = new CarAction.Builder()
            .SetTitle("Start drive")
            .SetOnClickListener(new ClickListener(StartNavigation))
            .Build();

        return new PlaceListMapTemplate.Builder()
            .SetTitle(state.Mode == ConnectionMode.NoTrip ? "This morning" : state.Trip.Title)
            .SetHeaderAction(CarAction.AppIcon)
            .SetItemList(items.Build())
            .SetActionStrip(new ActionStrip.Builder().AddAction(startDrive).Build())
            .SetAnchor(school)
            .Build();
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
