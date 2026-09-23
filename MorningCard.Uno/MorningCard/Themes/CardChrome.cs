namespace MorningCard.Themes;

/// <summary>
/// The live design dials: corner radius, motion speed and reduced motion.
///
/// These cut across the visual tree - the radius reaches a Border the simulator has no handle
/// on - and they are demo controls rather than app
/// state, so they live here instead of in a model. Consumers subscribe to <see cref="Changed"/>.
/// </summary>
public static class CardChrome
{
    private static double _cornerRadius = 34d;
    private static double _motionSpeed = 1d;
    private static bool _reducedMotion;

    public static event EventHandler? Changed;

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

    /// <summary>Every entrance duration is divided by this, 0.25 to 2.</summary>
    public static double MotionSpeed
    {
        get => _motionSpeed;
        set
        {
            if (Math.Abs(_motionSpeed - value) < 0.01d)
            {
                return;
            }

            _motionSpeed = Math.Max(0.25d, value);
            Raise();
        }
    }

    /// <summary>
    /// The only reduced-motion signal on desktop: UISettings.AnimationsEnabled is hardcoded true
    /// there, so the OS cannot be asked. On Android the two are combined.
    /// </summary>
    public static bool ReducedMotion
    {
        get => _reducedMotion;
        set
        {
            if (_reducedMotion == value)
            {
                return;
            }

            _reducedMotion = value;
            Raise();
        }
    }

    private static void Raise() => Changed?.Invoke(null, EventArgs.Empty);
}
