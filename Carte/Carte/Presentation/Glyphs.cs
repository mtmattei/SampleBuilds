using Microsoft.UI.Xaml.Media.Imaging;

namespace Carte.Presentation;

// Lucide glyphs from Assets/Svg. Ink for paper backgrounds, paper inside the selected ink disc.
public static class Glyphs
{
    private static readonly Dictionary<string, SvgImageSource> Cache = new(StringComparer.Ordinal);

    public static SvgImageSource Ink(string name) => Get(name);
    public static SvgImageSource Paper(string name) => Get(name + "-paper");

    private static SvgImageSource Get(string file)
    {
        if (!Cache.TryGetValue(file, out var source))
            Cache[file] = source = new SvgImageSource(new Uri($"ms-appx:///Assets/Svg/{file}.svg"));
        return source;
    }
}
