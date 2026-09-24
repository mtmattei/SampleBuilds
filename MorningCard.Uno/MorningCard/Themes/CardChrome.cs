namespace MorningCard.Themes;

/// <summary>
/// The live design dials: accent hue and corner radius.
///
/// These cut across the visual tree - the hue reaches XAML brushes and a Skia canvas, the radius
/// reaches a Border the simulator has no handle on - and they are demo controls rather than app
/// state, so they live here instead of in a model. Consumers subscribe to <see cref="Changed"/>.
/// </summary>
public static class CardChrome
{
    private static double _hue = AccentPalette.DefaultHue;
    private static double _cornerRadius = 28d;

    public static event EventHandler? Changed;

    /// <summary>The route accent, 0 to 360.</summary>
    public static double Hue
    {
        get => _hue;
        set
        {
            if (Math.Abs(_hue - value) < 0.5d)
            {
                return;
            }

            _hue = value;
            AccentPalette.Apply(value);
            Raise();
        }
    }

    /// <summary>The card's corner, 12 to 44 epx.</summary>
    public static double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (Math.Abs(_cornerRadius - value) < 0.5d)
            {
                return;
            }

            _cornerRadius = value;
            Raise();
        }
    }

    private static void Raise() => Changed?.Invoke(null, EventArgs.Empty);
}
