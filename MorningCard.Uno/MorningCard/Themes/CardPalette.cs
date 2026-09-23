namespace MorningCard.Themes;

/// <summary>
/// Reads card tokens for code that has no element to resolve a ThemeResource against: converters
/// and the Skia canvases. The app is light only (v6 decision B6), so this is a plain lookup in the
/// Light theme dictionary, walking the merged dictionaries.
/// </summary>
public static class CardPalette
{
    /// <summary>The token's brush, or null when the key is unknown.</summary>
    public static Brush? Brush(string key) => Lookup(key) as Brush;

    /// <summary>The value stored under <paramref name="key"/> in the Light theme dictionary.</summary>
    public static object? Lookup(string key)
    {
        if (Application.Current?.Resources is not { } resources)
        {
            return null;
        }

        foreach (var dictionary in Flatten(resources))
        {
            if (dictionary.ThemeDictionaries.TryGetValue("Light", out var themed)
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
}
