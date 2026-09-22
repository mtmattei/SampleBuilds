using Windows.UI;

namespace MorningCard.Themes;

/// <summary>
/// Writes the route accent into the app's brushes at runtime, which is what the hue dial moves.
///
/// Material's seed (<c>SemanticThemeHelper.PrimarySeed</c>) would be the idiomatic route, but it
/// derives HCT tones that do not land on the design's exact hsl() values. Mutating the app-level
/// brush instances hits them exactly; the price is that Material controls only follow along where
/// their lightweight-styling keys point at these same brushes.
/// </summary>
public static class AccentPalette
{
    /// <summary>The hue the design ships with.</summary>
    public const double DefaultHue = 218d;

    /// <summary>
    /// The theme the card is currently drawn in.
    ///
    /// Converters have no element to resolve against, and the application-level resource lookup
    /// answers for the application's theme rather than the root element's - which is the one the
    /// theme service switches. Reading colours through this kept the stats row and the ETA number
    /// from rendering dark ink on a dark card. Elements that do have a theme (the Skia canvas) use
    /// their own ActualTheme instead.
    /// </summary>
    public static ElementTheme CurrentTheme { get; private set; } = ElementTheme.Light;

    /// <summary>Follows <paramref name="root"/>'s theme for the life of the app.</summary>
    public static void Track(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        CurrentTheme = root.ActualTheme;
        root.ActualThemeChanged += (sender, _) => CurrentTheme = sender.ActualTheme;
    }

    /// <summary>A brush from the current theme's dictionary, or null when the key is unknown.</summary>
    public static Brush? Brush(string key) => Lookup(key, CurrentTheme) as Brush;

    private const string AccentKey = "AccentBrush";
    private const string AccentInkKey = "AccentInkBrush";

    /// <summary>
    /// Applies <paramref name="hue"/> to the accent brushes in both theme dictionaries, so a later
    /// theme switch keeps the dialled hue. Saturation and lightness are fixed by the design.
    /// </summary>
    public static void Apply(double hue)
    {
        foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
        {
            SetBrush(AccentKey, Accent(hue, theme), theme);
            SetBrush(AccentInkKey, AccentInk(hue, theme), theme);
        }
    }

    public static Color Accent(double hue, ElementTheme theme) => theme == ElementTheme.Dark
        ? FromHsl(hue, 0.90, 0.66)
        : FromHsl(hue, 0.82, 0.52);

    public static Color AccentInk(double hue, ElementTheme theme) => theme == ElementTheme.Dark
        ? FromHsl(hue, 0.90, 0.72)
        : FromHsl(hue, 0.70, 0.42);

    /// <summary>
    /// Looks a key up in the theme dictionary for <paramref name="theme"/>, walking the merged
    /// dictionaries. The application-level indexer resolves against the application's theme, which
    /// is not the one the card is drawn in once the theme service has switched the root element.
    /// </summary>
    public static object? Lookup(string key, ElementTheme theme)
    {
        var themeKey = theme == ElementTheme.Dark ? "Dark" : "Light";

        if (Application.Current?.Resources is not { } resources)
        {
            return null;
        }

        foreach (var dictionary in Flatten(resources))
        {
            if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var themed)
                && themed is ResourceDictionary themedDictionary
                && themedDictionary.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<ResourceDictionary> Flatten(ResourceDictionary root)
    {
        yield return root;

        foreach (var merged in root.MergedDictionaries)
        {
            foreach (var nested in Flatten(merged))
            {
                yield return nested;
            }
        }
    }

    private static void SetBrush(string key, Color color, ElementTheme theme)
    {
        if (Lookup(key, theme) is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    /// <summary>CSS <c>hsl()</c> to RGB, so the tokens and this helper agree to the byte.</summary>
    public static Color FromHsl(double hueDegrees, double saturation, double lightness)
    {
        var hue = ((hueDegrees % 360d) + 360d) % 360d;
        var chroma = (1d - Math.Abs((2d * lightness) - 1d)) * saturation;
        var sector = hue / 60d;
        var second = chroma * (1d - Math.Abs((sector % 2d) - 1d));
        var offset = lightness - (chroma / 2d);

        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, second, 0d),
            1 => (second, chroma, 0d),
            2 => (0d, chroma, second),
            3 => (0d, second, chroma),
            4 => (second, 0d, chroma),
            _ => (chroma, 0d, second),
        };

        return Color.FromArgb(
            255,
            ToByte(r + offset),
            ToByte(g + offset),
            ToByte(b + offset));
    }

    private static byte ToByte(double channel) => (byte)Math.Clamp(Math.Round(channel * 255d), 0d, 255d);
}
