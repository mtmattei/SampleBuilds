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

/// <summary>Maps a stat's severity onto the ink it is drawn in.</summary>
public sealed class StatLevelBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        StatLevel.Bad => Tokens.Of("DangerInkBrush"),
        StatLevel.Warn => Tokens.Of("AmberInkBrush"),
        _ => Tokens.Of("InkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The range bar's fill follows the same severity as its value.</summary>
public sealed class StatLevelFillConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        StatLevel.Bad => Tokens.Of("DangerBrush"),
        StatLevel.Warn => Tokens.Of("AmberBrush"),
        _ => Tokens.Of("GoodBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
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
/// The accented span inside the headline, in the tone's ink: slate on time, brown to leave now,
/// brick when late (v6).
/// </summary>
public sealed class AccentToneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        CardTone.Late => Tokens.Of("DangerInkBrush"),
        CardTone.LeaveNow => Tokens.Of("LeaveNowInkBrush"),
        _ => Tokens.Of("AccentInkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// The state badge's ground. A tint rather than a solid fill, so the badge reads as a label on the
/// card and not as a button someone forgot to wire up.
/// </summary>
public sealed class ToneChipBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        CardTone.Late => Tokens.Of("DangerChipBrush"),
        CardTone.LeaveNow => Tokens.Of("AmberChipBrush"),
        _ => Tokens.Of("AccentChipBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The ink on that ground, and on the timeline's first column.</summary>
public sealed class ToneChipInkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        CardTone.Late => Tokens.Of("DangerInkBrush"),
        CardTone.LeaveNow => Tokens.Of("LeaveNowInkBrush"),
        _ => Tokens.Of("AccentInkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The timeline track's drive segment and its "now" marker.</summary>
public sealed class ToneTrackBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        CardTone.Late => Tokens.Of("DangerBrush"),
        CardTone.LeaveNow => Tokens.Of("LeaveNowBrush"),
        _ => Tokens.Of("AccentBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Running late turns the headline red; everything else reads as secondary ink.</summary>
public sealed class ToneBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CardTone.Late
            ? Tokens.Of("DangerInkBrush")
            : Tokens.Of("Ink2Brush");

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// The cabin's readiness dot. Amber while it still wants doing, green once it is handled - the one
/// check on the list the driver can actually act on.
/// </summary>
public sealed class CheckDotBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        Tokens.Of(value is true ? "AmberBrush" : "GoodBrush");

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The big arrival number goes red when the arrival is after the bell.</summary>
public sealed class LateInkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true
            ? Tokens.Of("DangerInkBrush")
            : Tokens.Of("InkBrush");

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

/// <summary>
/// Shows or hides one of two glyphs held in the same cell. Cross-fading opacity is the only way to
/// swap an icon without a flick: Path.Data and FontIcon.Glyph are strings, not animatable.
/// </summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is true;

        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? 1d : 0d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Points the checklist's chevron at what tapping it does: down to open, up once open. Rotating one
/// glyph rather than swapping two keeps a single asset, which is the rule for icon states.
/// </summary>
/// <summary>
/// The checklist header's corners. Rounded all round while it is closed, because then the header
/// IS the container; flat along the bottom once it is open, because the rows continue past it and
/// a hover fill that curved away mid-list read as a second container starting underneath.
/// </summary>
public sealed class ChecksHeaderRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? new CornerRadius(24, 24, 0, 0) : new CornerRadius(24);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
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

/// <summary>Stale data dims the whole stats row without hiding it.</summary>
public sealed class StaleOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? 0.45d : 1d;

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

/// <summary>Turns a 0-100 percentage into a star weight, so the range bar fills by layout.</summary>
public sealed class PercentToStarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var percent = value is double d ? d : 0d;

        if (string.Equals(parameter as string, "Remainder", StringComparison.OrdinalIgnoreCase))
        {
            percent = 100d - percent;
        }

        return new GridLength(Math.Max(0d, percent), GridUnitType.Star);
    }

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
