using System.Text.Json;
using System.Text.RegularExpressions;

namespace Carte.Core;

// File-backed IMenuStore. One JSON file per menu and per shortlist, keyed by menu ID.
public sealed partial class FileMenuStore : IMenuStore
{
    private readonly string _root;

    public FileMenuStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _root = rootDirectory;
    }

    public Task SaveMenuAsync(MenuDocument menu, CancellationToken cancellationToken)
    {
        MenuValidation.Validate(menu);
        return WriteAtomicAsync(PathFor("menus", menu.Id), menu.ToJson(), cancellationToken);
    }

    // A missing or invalid saved menu returns null so the caller can fall back to a fresh copy.
    public async Task<MenuDocument?> LoadMenuAsync(string menuId, CancellationToken cancellationToken)
    {
        var path = PathFor("menus", menuId);
        if (!File.Exists(path)) return null;
        try
        {
            return MenuDocument.FromJson(await File.ReadAllTextAsync(path, cancellationToken));
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public Task SaveShortlistAsync(string menuId, IReadOnlyCollection<string> itemIds, CancellationToken cancellationToken) =>
        WriteAtomicAsync(PathFor("shortlists", menuId), JsonSerializer.Serialize(itemIds), cancellationToken);

    public async Task<IReadOnlyList<string>> LoadShortlistAsync(string menuId, CancellationToken cancellationToken)
    {
        var path = PathFor("shortlists", menuId);
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(path, cancellationToken)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private string PathFor(string folder, string menuId)
    {
        if (string.IsNullOrEmpty(menuId) || !SafeId().IsMatch(menuId))
            throw new ArgumentException("Menu IDs may contain only letters, digits, '-' and '_'.", nameof(menuId));
        return Path.Combine(_root, folder, menuId + ".json");
    }

    // Write beside the target, then move over it, so a crash never leaves half a file.
    private static async Task WriteAtomicAsync(string path, string contents, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, contents, cancellationToken);
        File.Move(temp, path, overwrite: true);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,80}$")]
    private static partial Regex SafeId();
}
