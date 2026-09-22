using Microsoft.UI.Xaml.Data;
using MorningCard.Core.Models;

namespace MorningCard.Presentation;

/// <summary>Maps a stat's severity onto the ink it is drawn in.</summary>
public sealed class StatLevelBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        StatLevel.Bad => Brush("DangerInkBrush"),
        StatLevel.Warn => Brush("AmberInkBrush"),
        _ => Brush("InkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    internal static object Brush(string key) => Application.Current.Resources[key];
}

/// <summary>The range bar's fill follows the same severity as its value.</summary>
public sealed class StatLevelFillConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        StatLevel.Bad => StatLevelBrushConverter.Brush("DangerBrush"),
        StatLevel.Warn => StatLevelBrushConverter.Brush("AmberBrush"),
        _ => StatLevelBrushConverter.Brush("GoodBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Traffic keeps green, amber and red apart from the route accent on purpose.</summary>
public sealed class TrafficBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        TrafficLevel.Heavy => StatLevelBrushConverter.Brush("DangerInkBrush"),
        TrafficLevel.Moderate => StatLevelBrushConverter.Brush("AmberInkBrush"),
        _ => StatLevelBrushConverter.Brush("GoodInkBrush"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Running late turns the headline red; everything else reads as secondary ink.</summary>
public sealed class ToneBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CardTone.Late
            ? StatLevelBrushConverter.Brush("DangerInkBrush")
            : StatLevelBrushConverter.Brush("Ink2Brush");

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>The big arrival number goes red when the arrival is after the bell.</summary>
public sealed class LateInkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true
            ? StatLevelBrushConverter.Brush("DangerInkBrush")
            : StatLevelBrushConverter.Brush("InkBrush");

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
