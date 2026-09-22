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

    private const string AccentKey = "AccentBrush";
    private const string AccentInkKey = "AccentInkBrush";

    /// <summary>
    /// Applies <paramref name="hue"/> to the accent brushes for the given theme. Saturation and
    /// lightness are fixed by the design; only the hue is dialable.
    /// </summary>
    public static void Apply(ResourceDictionary resources, double hue, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var isDark = theme == ElementTheme.Dark;

        SetBrush(resources, AccentKey, isDark
            ? FromHsl(hue, 0.90, 0.66)
            : FromHsl(hue, 0.82, 0.52));

        SetBrush(resources, AccentInkKey, isDark
            ? FromHsl(hue, 0.90, 0.72)
            : FromHsl(hue, 0.70, 0.42));
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        if (resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
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
