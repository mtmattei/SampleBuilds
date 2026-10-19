using Windows.ApplicationModel.Resources;

namespace Carte.Presentation;

// Value strings for view models. Static UI text uses x:Uid with the same Strings/en/Resources.resw.
public static class Loc
{
    private static readonly ResourceLoader Loader = ResourceLoader.GetForViewIndependentUse();

    // The resource map stores "ItemDetail.Save.Content" as "ItemDetail/Save.Content": the first dot becomes a slash.
    public static string Get(string key)
    {
        var firstDot = key.IndexOf('.');
        var value = firstDot > 0 ? Loader.GetString(key[..firstDot] + "/" + key[(firstDot + 1)..]) : "";
        if (string.IsNullOrEmpty(value)) value = Loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public static string Format(string key, params object[] args) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, Get(key), args);
}
