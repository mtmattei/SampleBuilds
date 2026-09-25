using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Text;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using AndroidX.Car.App.Validation;
using AndroidX.Core.Graphics.Drawable;
using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Services;
using MorningCard.Presentation;
using CarAction = AndroidX.Car.App.Model.Action;

namespace MorningCard.Droid.Car;

/// <summary>
/// Android Auto: the morning's drop-off as a pane (Uno AndroidAuto opt-in, AndroidX Car App
/// Library). A POI app gets host-drawn templates only, so this is the closest the car comes to the
/// v6 panel: the trip, fuel, cabin and parking as rows with the card's Lucide icons, the car photo
/// as the pane image, Start drive as the primary button. No map: the map belongs to the navigation
/// app that Start drive hands off to.
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

/// <summary>One screen, rebuilt whenever the hub changes.</summary>
internal sealed class DropoffScreen : Screen
{
    // Lucide glyphs (Themes/Icons.xaml), the card's icon set.
    private const string GlyphSchool = "\uE234";
    private const string GlyphFuel = "\uE2AF";
    private const string GlyphThermometer = "\uE186";
    private const string GlyphParking = "\uE3C9";

    // The card's tokens (CardTokens.xaml). The car session cannot read XAML resources - the Uno app
    // may never have started - so the values are repeated here, each with a lighter night variant.
    private static readonly CarColor Accent = Custom(0xFF4A6B8A, 0xFF9DB6CE);
    private static readonly CarColor Good = Custom(0xFF4F6B4C, 0xFFA9C2A5);
    private static readonly CarColor Amber = Custom(0xFF8A6236, 0xFFD9B48A);
    private static readonly CarColor Danger = Custom(0xFF9C4A3E, 0xFFE3998D);

    private readonly Handler _main = new(Looper.MainLooper!);
    private readonly CancellationTokenSource _watch = new();
    private readonly Dictionary<string, IconCompat> _glyphs = new();
    private Typeface? _lucide;
    private IconCompat? _carPhoto;

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
        var car = state.Vehicle;
        var noTrip = state.Mode == ConnectionMode.NoTrip;
        var pane = new Pane.Builder();

        if (noTrip)
        {
            pane.AddRow(new Row.Builder()
                .SetTitle("No drop-off today")
                .AddText("School is closed.")
                .SetImage(Glyph(GlyphSchool, Accent))
                .Build());
        }
        else
        {
            var plan = state.Trip;
            var timeline = TripMath.Derive(plan, TripMath.FromMinutes(state.ClockMinutes));
            var (lead, accent, tone) = timeline.Tone switch
            {
                CardTone.Late => ("Running ", $"{Math.Max(0, TripMath.ToMinutes(timeline.Arrive) + plan.WalkMin - TripMath.ToMinutes(plan.Bell))} min late", Danger),
                CardTone.LeaveNow => ("Leave ", "now", Amber),
                _ => ("Leave in ", $"{timeline.SlackMin} min", Accent),
            };
            var (traffic, trafficColor) = timeline.Traffic switch
            {
                TrafficLevel.Heavy => ("Heavy traffic", Danger),
                TrafficLevel.Moderate => ("Moderate traffic", Amber),
                _ => ("Light traffic", Good),
            };

            // The trip: the headline, the drive, and the three times. Row titles take no colour
            // spans (the host throws "CarSpan type is not allowed"), so the tone rides the icon.
            pane.AddRow(new Row.Builder()
                .SetTitle(lead + accent)
                .AddText(Colored($"Drive {timeline.DriveMin} min · ", traffic, trafficColor))
                .AddText($"Leave by {Clock(timeline.LeaveBy)} · arrive {Clock(timeline.Arrive)} · bell {Clock(plan.Bell)}")
                .SetImage(Glyph(GlyphSchool, tone))
                .Build());
        }

        pane.AddRow(new Row.Builder()
            .SetTitle($"{car.Range} km of range")
            .AddText(car.IsStale ? $"Last read {Clock(car.LastSeen)}" : "Fuel this week")
            .SetImage(Glyph(GlyphFuel, Amber))
            .Build());

        pane.AddRow(new Row.Builder()
            .SetTitle("Warm the cabin")
            .AddText(car.IsStale ? "Car offline" : car.ClimateOn ? "Warming" : $"Suggested · outside is {car.OutsideTemp}°")
            .SetImage(Glyph(GlyphThermometer, Accent))
            .Build());

        if (!noTrip)
        {
            pane.AddRow(new Row.Builder()
                .SetTitle(state.Trip.ParkingStreet)
                .AddText(state.Trip.ParkingNote)
                .SetImage(Glyph(GlyphParking, Good))
                .Build());

            pane.AddAction(new CarAction.Builder()
                .SetTitle("Start drive")
                .SetFlags(CarAction.FlagPrimary)
                .SetBackgroundColor(Accent)
                .SetOnClickListener(new ClickListener(StartNavigation))
                .Build());
        }

        // The card's cabin Start, while undecided and the car can act on it.
        if (!car.IsStale && !car.ClimateOn)
        {
            pane.AddAction(new CarAction.Builder()
                .SetTitle("Start warming")
                .SetOnClickListener(new ClickListener(() => _ = SharedHub.Instance.SetClimateAsync(true, CancellationToken.None)))
                .Build());
        }

        // The car photo, as on the card (the XC60, decision C6).
        if (CarPhoto() is { } photo)
        {
            pane.SetImage(new CarIcon.Builder(photo).Build());
        }

        // The card footer's "Outback · Locked" and its lock button, in the header strip.
        var lockAction = new CarAction.Builder()
            .SetTitle(car.IsLocked ? $"{car.Name} · Unlock" : $"{car.Name} · Lock")
            .SetOnClickListener(new ClickListener(() => _ = SharedHub.Instance.SetLockedAsync(!car.IsLocked, CancellationToken.None)))
            .Build();

        return new PaneTemplate.Builder(pane.Build())
            .SetTitle(noTrip ? "This morning" : state.Trip.Title)
            .SetHeaderAction(CarAction.AppIcon)
            .SetActionStrip(new ActionStrip.Builder().AddAction(lockAction).Build())
            .Build();
    }

    /// <summary>Hands the drop-off to the car's navigation app (Car App Library ACTION_NAVIGATE).</summary>
    private void StartNavigation()
    {
        var geo = Android.Net.Uri.Parse(FormattableString.Invariant($"geo:{Destination.Latitude},{Destination.Longitude}"));
        CarContext.StartCarApp(new Intent(CarContext.ActionNavigate, geo));
    }

    /// <summary>A sentence with one span in a colour: "Leave in " + "9 min".</summary>
    private static SpannableString Colored(string lead, string accent, CarColor color)
    {
        var text = new SpannableString(lead + accent);
        text.SetSpan(ForegroundCarColorSpan.Create(color), lead.Length, lead.Length + accent.Length, SpanTypes.ExclusiveExclusive);
        return text;
    }

    /// <summary>
    /// A Lucide glyph from the card's own icon font, rendered once to a bitmap and tinted by the
    /// host: the card's icons, not a second icon set.
    /// </summary>
    private CarIcon Glyph(string glyph, CarColor tint)
    {
        if (!_glyphs.TryGetValue(glyph, out var icon))
        {
            _lucide ??= Typeface.CreateFromAsset(CarContext.Assets, "Assets/Fonts/lucide.ttf");
            const int size = 96;
            var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!)!;
            using var canvas = new Android.Graphics.Canvas(bitmap);
            using var paint = new Paint(PaintFlags.AntiAlias) { Color = Android.Graphics.Color.White, TextSize = size * 0.9f, TextAlign = Paint.Align.Center };
            paint.SetTypeface(_lucide);
            var metrics = paint.GetFontMetrics()!;
            canvas.DrawText(glyph, size / 2f, (size / 2f) - ((metrics.Ascent + metrics.Descent) / 2f), paint);
            icon = IconCompat.CreateWithBitmap(bitmap)!;
            _glyphs[glyph] = icon;
        }

        return new CarIcon.Builder(icon).SetTint(tint).Build();
    }

    /// <summary>
    /// The card's car photo, fitted into a square with clear margins: the host crops the pane image
    /// to a square, which cut the wide photo down to the car's nose. Resizetizer packs the asset as
    /// a drawable (assets_car_xc60).
    /// </summary>
    private IconCompat? CarPhoto()
    {
        if (_carPhoto is not null)
        {
            return _carPhoto;
        }

        var id = CarContext.Resources!.GetIdentifier("assets_car_xc60", "drawable", CarContext.PackageName);
        if (id == 0 || BitmapFactory.DecodeResource(CarContext.Resources, id) is not { } source)
        {
            return null;
        }

        var side = Math.Max(source.Width, source.Height);
        var square = Bitmap.CreateBitmap(side, side, Bitmap.Config.Argb8888!)!;
        using (var canvas = new Android.Graphics.Canvas(square))
        {
            canvas.DrawBitmap(source, (side - source.Width) / 2f, (side - source.Height) / 2f, null);
        }

        source.Recycle();
        return _carPhoto = IconCompat.CreateWithBitmap(square)!;
    }

    private static CarColor Custom(uint day, uint night) => CarColor.CreateCustom(unchecked((int)day), unchecked((int)night));

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
