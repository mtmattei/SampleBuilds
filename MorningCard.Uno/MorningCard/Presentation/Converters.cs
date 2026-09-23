using Microsoft.UI.Xaml.Data;
using MorningCard.Core.Models;
using MorningCard.Themes;

namespace MorningCard.Presentation;

/// <summary>
/// A card token's brush. A converter has no element to resolve a ThemeResource against.
/// </summary>
internal static class Tokens
{
    public static object Of(string key) => CardPalette.Brush(key)!;
}

/// <summary>Traffic keeps green, amber and red apart from the route accent on purpose.</summary>
public sealed class TrafficBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        TrafficLevel.Heavy => Tokens.Of("DangerInkBrush"),
        TrafficLevel.Moderate => Tokens.Of("AmberInkBrush"),
        _ => Tokens.Of("GoodInkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Negates a flag, for the commands that stay off while the car is unreachable. A visibility
/// converter cannot stand in here: handing a Visibility to IsEnabled silently leaves the control
/// enabled, which is how the offline state shipped with four live buttons.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is not true;
}

/// <summary>A chevron's rotation when open: 180 by default, or the angle given as the parameter (90 for rows).</summary>
public sealed class BoolToAngleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true
            ? double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) ? angle : 180d
            : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Pass "Invert" as the parameter to flip the test.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is true;

        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Hides a slot when its text is absent, e.g. the second line under an action.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The cabin pill's ground: neutral while suggested or skipped, slate-tinted while warming.</summary>
public sealed class CabinPillBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        Tokens.Of(value is CabinChoice.Warming ? "WarmingChipBrush" : "NeutralChipBrush");

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The cabin pill's ink.</summary>
public sealed class CabinPillInkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        CabinChoice.Warming => Tokens.Of("AccentInkBrush"),
        CabinChoice.Skipped => Tokens.Of("CapsBrush"),
        _ => Tokens.Of("NeutralChipInkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The cabin row's icon: grey pending, slate warming, faint once skipped.</summary>
public sealed class CabinIconBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        CabinChoice.Warming => Tokens.Of("AccentBrush"),
        CabinChoice.Skipped => Tokens.Of("FaintBrush"),
        _ => Tokens.Of("NeutralChipInkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The fuel pill's ground by severity: sage fine, ochre soon, brick now.</summary>
public sealed class FuelPillBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        StatLevel.Bad => Tokens.Of("DangerChipBrush"),
        StatLevel.Warn => Tokens.Of("AmberChipBrush"),
        _ => Tokens.Of("GoodChipBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The fuel pill's ink, and the fuel icon.</summary>
public sealed class FuelPillInkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        StatLevel.Bad => Tokens.Of("DangerInkBrush"),
        StatLevel.Warn => Tokens.Of("AmberInkBrush"),
        _ => Tokens.Of("GoodInkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>A skipped decision is struck through, not removed.</summary>
public sealed class BoolToStrikeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>A token's brush by key, for views that carry the key rather than a tone.</summary>
public sealed class TokenBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        Tokens.Of(value as string ?? "InkBrush");

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// The watch's headline emphasis (v6 Wear ramp): the countdown the phone sets at 27 becomes 38; an
/// inline time stays at the sentence's 17.
/// </summary>
public sealed class WatchAccentSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is double size && size >= 20 ? 38d : 17d;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
