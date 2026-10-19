using System.Text.Json;

namespace Carte.Core;

public sealed record AxisDefinition(string Id, string Low, string High);
public sealed record AxisSet(string Id, int Revision, AxisDefinition X, AxisDefinition Y, bool Locked);
public sealed record MenuCategory(string Id, string Label, string Glyph, AxisSet AxisSet);
public sealed record MenuPrice(decimal Amount, string Currency, string Unit);
public sealed record TastePlacement(double X, double Y, string AxisSetId, int AxisRevision, string Confidence, string Rationale);
public sealed record MenuSource(string Kind, int PageCount);
public sealed record MenuItem(string Id, string CategoryId, string Name, string ShortName, string Glyph,
    MenuPrice? Price, IReadOnlyList<string> SensoryTags, string Description, string OriginalMenuText,
    TastePlacement? Placement, IReadOnlyList<string> PairingItemIds);
public sealed record MenuDocument(int SchemaVersion, string Id, string Name, string Locale, string Currency,
    bool IsIllustrative, MenuSource Source, IReadOnlyList<MenuCategory> Categories, IReadOnlyList<MenuItem> Items)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Validate incoming JSON against menu.schema.json before calling this domain loader.
    public static MenuDocument FromJson(string json)
    {
        var menu = JsonSerializer.Deserialize<MenuDocument>(json, JsonOptions)
            ?? throw new JsonException("Menu JSON was empty.");
        MenuValidation.Validate(menu);
        return menu;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

public static class MenuValidation
{
    public static void Validate(MenuDocument menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (menu.SchemaVersion != 1) throw new ArgumentException("Unsupported menu schema.");
        if (menu.Items is null || menu.Categories is null || menu.Categories.Count == 0)
            throw new ArgumentException("Menu categories and items are required.");
        if (string.IsNullOrWhiteSpace(menu.Id) || string.IsNullOrWhiteSpace(menu.Currency))
            throw new ArgumentException("Menu ID and currency are required.");
        if (menu.Categories.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != menu.Categories.Count)
            throw new ArgumentException("Category IDs must be unique.");
        if (menu.Items.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != menu.Items.Count)
            throw new ArgumentException("Item IDs must be unique.");
        var categories = menu.Categories.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var ids = menu.Items.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in menu.Items)
        {
            if (!categories.TryGetValue(item.CategoryId, out var category))
                throw new ArgumentException($"Unknown category for {item.Id}.");
            if (item.Price is { } price && (price.Amount < 0 || price.Currency != menu.Currency))
                throw new ArgumentException($"Invalid price for {item.Id}.");
            if (item.Placement is { } p)
            {
                TasteMapGeometry.ValidateCoordinate(p.X, nameof(p.X));
                TasteMapGeometry.ValidateCoordinate(p.Y, nameof(p.Y));
                if (p.AxisSetId != category.AxisSet.Id || p.AxisRevision != category.AxisSet.Revision)
                    throw new ArgumentException($"Stale axis calibration for {item.Id}.");
                if (p.Confidence is not ("estimated" or "low" or "reviewed"))
                    throw new ArgumentException($"Unknown confidence for {item.Id}.");
                if (string.IsNullOrWhiteSpace(p.Rationale))
                    throw new ArgumentException($"Placement rationale missing for {item.Id}.");
            }
            if (item.PairingItemIds is null || item.PairingItemIds.Any(id => !ids.Contains(id)))
                throw new ArgumentException($"Unknown pairing for {item.Id}.");
        }
    }
}
