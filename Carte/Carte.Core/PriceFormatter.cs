using System.Globalization;

namespace Carte.Core;

// Locale-aware menu prices. The currency symbol is used only when it belongs to the menu's locale;
// otherwise the ISO code is shown so a symbol is never guessed.
public static class PriceFormatter
{
    public static string FormatAmount(decimal amount, string currency, string locale)
    {
        var culture = CultureFor(locale);
        var decimals = amount % 1 == 0 ? 0 : 2;
        return LocalCurrency(culture) == currency
            ? amount.ToString("C" + decimals, culture)
            : $"{amount.ToString("N" + decimals, culture)} {currency}";
    }

    // "item" is the implicit unit; glass, bottle and similar serving units are preserved.
    public static string Format(MenuPrice price, string locale)
    {
        ArgumentNullException.ThrowIfNull(price);
        var amount = FormatAmount(price.Amount, price.Currency, locale);
        return string.IsNullOrWhiteSpace(price.Unit) || price.Unit == "item" ? amount : $"{amount} / {price.Unit}";
    }

    private static CultureInfo CultureFor(string locale)
    {
        try { return CultureInfo.GetCultureInfo(locale); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }

    private static string? LocalCurrency(CultureInfo culture)
    {
        try { return culture.IsNeutralCulture || culture.Equals(CultureInfo.InvariantCulture) ? null : new RegionInfo(culture.Name).ISOCurrencySymbol; }
        catch (ArgumentException) { return null; }
    }
}
