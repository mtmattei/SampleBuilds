using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Services;
using MorningCard.Themes;
using Uno.Extensions.Toolkit;

namespace MorningCard.Presentation;

/// <summary>
/// The Simulator: every control writes to the simulated hub or to the live design dials, and the
/// card reads the result. Nothing here reaches into the card, which is the same contract the
/// prototype's panel had.
///
/// It ships in every build. In a sample this is the only way to see the late, warning, offline,
/// loading and empty states, so it is part of what the app demonstrates - and its title says
/// "Simulator" so nothing on the card pretends to be a real car.
/// </summary>
public partial record TuningModel
{
    private readonly IThemeService _theme;

    public TuningModel(SimulatedDriveHub hub, IThemeService theme)
    {
        _theme = theme;

        var state = hub.State;

        Mode = Forward(Options.First(option => option.Value == state.Mode), option => hub.SetMode(option.Value));
        Clock = Forward(state.ClockMinutes, hub.SetClock);
        TrafficDelay = Forward(state.Trip.DelayMin, hub.SetTrafficDelay);
        Range = Forward(state.Vehicle.Range, hub.SetRange);
        CabinTemp = Forward(state.Vehicle.CabinTemp, hub.SetCabinTemp);
        ChargePercent = Forward(state.Vehicle.ChargePercent, hub.SetChargePercent);
        PluggedIn = Forward(state.Vehicle.IsPluggedIn, hub.SetPluggedIn);

        Hue = Forward(CardChrome.Hue, value => CardChrome.Hue = value);
        CornerRadius = Forward(CardChrome.CornerRadius, value => CardChrome.CornerRadius = value);
        MotionSpeed = Forward(CardChrome.MotionSpeed, value => CardChrome.MotionSpeed = value);
        ReducedMotion = Forward(CardChrome.ReducedMotion, value => CardChrome.ReducedMotion = value);
    }

    /// <summary>A two-way state whose every change is pushed to <paramref name="apply"/>.</summary>
    private IState<T> Forward<T>(T initial, Action<T> apply)
        where T : notnull
    {
        // ForEach, not the deprecated ForEachAsync: it returns the state and roots the subscription
        // through AttachedProperty, so no hand-held disposable list is needed to keep forwarding
        // alive past the next GC.
        return State.Value(this, () => initial).ForEach((value, _) =>
        {
            // A state can report no value at all; there is nothing to forward then.
            if (value is not null)
            {
                apply(value);
            }

            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// Sliders report doubles, so the int knobs are bridged here rather than with an int state,
    /// which would round on every tick of a drag.
    /// </summary>
    private IState<double> Forward(int initial, Action<int> apply) =>
        Forward((double)initial, value => apply((int)Math.Round(value)));

    public IState<ModeOption> Mode { get; }

    /// <summary>
    /// Minutes since midnight. A double because Slider.Value is one, and an int state would round
    /// on every tick of the drag.
    /// </summary>
    public IState<double> Clock { get; }

    public IState<double> TrafficDelay { get; }

    public IState<double> Range { get; }

    public IState<double> CabinTemp { get; }

    public IState<bool> PluggedIn { get; }

    public IState<double> ChargePercent { get; }

    public IState<double> Hue { get; }

    public IState<double> CornerRadius { get; }

    public IState<double> MotionSpeed { get; }

    public IState<bool> ReducedMotion { get; }

    /// <summary>The four connection modes, in the order the design lists them.</summary>
    private static readonly IImmutableList<ModeOption> Options = ImmutableList.Create(
        new ModeOption(ConnectionMode.Live, "Live"),
        new ModeOption(ConnectionMode.Offline, "Offline"),
        new ModeOption(ConnectionMode.NoTrip, "No trip"));

    /// <summary>
    /// A plain property, not a feed: the list is four constants and never changes, so there is
    /// nothing for a feed to carry and no reason to make the chips wait on one.
    /// </summary>
    public IImmutableList<ModeOption> Modes => Options;

    /// <summary>Reads back as the clock, so the label under the slider is the time, not a count.</summary>
    public IFeed<string> ClockText => Clock.Select(value => TripMath.FromMinutes((int)value).ToString("H:mm", CultureInfo.InvariantCulture));

    public async ValueTask ReplayEntrance(CancellationToken ct)
    {
        CardMotion.RequestReplay();
        await ValueTask.CompletedTask;
    }

    public async ValueTask ToggleTheme(CancellationToken ct) =>
        await _theme.SetThemeAsync(_theme.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
}

