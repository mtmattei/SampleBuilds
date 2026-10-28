namespace Carte.Core;

public sealed record ShortlistSubtotal(decimal KnownAmount, string Currency, int MissingPriceCount);

public sealed class TonightShortlist
{
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    public IReadOnlyCollection<string> ItemIds => _ids.ToArray();

    public bool Toggle(string itemId, MenuDocument menu)
    {
        if (!menu.Items.Any(i => i.Id == itemId)) throw new ArgumentException("Unknown menu item.", nameof(itemId));
        if (_ids.Remove(itemId)) return false;
        _ids.Add(itemId);
        return true;
    }

    public void Restore(IEnumerable<string> ids, MenuDocument menu)
    {
        var validIds = menu.Items.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        _ids.Clear();
        foreach (var id in ids.Where(validIds.Contains)) _ids.Add(id);
    }

    public ShortlistSubtotal GetSubtotal(MenuDocument menu)
    {
        var items = menu.Items.Where(i => _ids.Contains(i.Id)).ToArray();
        if (items.Any(i => i.Price is { } p && p.Currency != menu.Currency))
            throw new InvalidOperationException("Cannot total multiple currencies.");
        return new(items.Sum(i => i.Price?.Amount ?? 0), menu.Currency, items.Count(i => i.Price is null));
    }
}
