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
    /// The element whose theme the card is drawn in. Converters have no element of their own to
    /// resolve a ThemeResource against, and the application-level lookup answers for the
    /// application's theme rather than the root element's - which is the one the theme service and
    /// the debug hook switch. Elements that do have a theme (the Skia canvas) use their own
    /// ActualTheme instead.
    /// </summary>
    private static FrameworkElement? _root;

    /// <summary>
    /// The brushes handed to converters, one live instance per token key.
    ///
    /// A converter runs once per binding evaluation and its result is never re-asked, so a brush
    /// picked out of a theme dictionary is a snapshot of whatever theme was current at first
    /// layout. That is always Light here: the page is built inside InitializeNavigationAsync, which
    /// finishes before there is a window root to track, so every converter-driven brush resolved
    /// Light on a dark card - the ETA number vanished and the stats row went dim. Handing out one
    /// live brush per key and repainting it on theme change makes the ordering irrelevant, because
    /// the instance XAML is holding is the one that changes colour.
    /// </summary>
    private static readonly Dictionary<string, SolidColorBrush> Live = [];

    public static ElementTheme CurrentTheme => _root?.ActualTheme ?? ElementTheme.Light;

    /// <summary>Follows <paramref name="root"/>'s theme for the life of the app.</summary>
    public static void Track(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        _root = root;
        root.ActualThemeChanged += (_, _) => Repaint();

        // The converters have already run by now, against no theme at all.
        Repaint();
    }

    /// <summary>
    /// A live brush for <paramref name="key"/>, repainted whenever the tracked root's theme
    /// changes. Null when the key is unknown, or when the token is not a solid colour.
    /// </summary>
    public static Brush? Brush(string key)
    {
        if (Live.TryGetValue(key, out var live))
        {
            return live;
        }

        if (Lookup(key, CurrentTheme) is not SolidColorBrush source)
        {
            return Lookup(key, CurrentTheme) as Brush;
        }

        // A copy, not the dictionary's own brush: repainting that one would rewrite the theme.
        live = new SolidColorBrush(source.Color);
        Live[key] = live;

        return live;
    }

    /// <summary>Pulls every live brush back to the colour its key carries in the current theme.</summary>
    private static void Repaint()
    {
        foreach (var (key, brush) in Live)
        {
            if (Lookup(key, CurrentTheme) is SolidColorBrush source)
            {
                brush.Color = source.Color;
            }
        }
    }

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

        // The live copies converters are holding do not follow the dictionary on their own.
        Repaint();
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
