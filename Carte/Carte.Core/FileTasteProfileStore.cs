using System.Text.Json;

namespace Carte.Core;

public sealed class FileTasteProfileStore(string directory)
{
    private readonly string _path = Path.Combine(directory, "taste-profile.json");
    private readonly SemaphoreSlim _writes = new(1, 1);

    public async Task<TasteProfile> LoadAsync()
    {
        if (!File.Exists(_path)) return TasteProfile.Empty;
        try
        {
            return (JsonSerializer.Deserialize(await File.ReadAllTextAsync(_path), CarteJsonContext.Default.TasteProfile)
                ?? TasteProfile.Empty).Normalize();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return TasteProfile.Empty;
        }
    }

    public async Task SaveAsync(TasteProfile profile)
    {
        await _writes.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path + ".tmp",
                JsonSerializer.Serialize(profile.Normalize(), CarteJsonContext.Default.TasteProfile));
            File.Move(_path + ".tmp", _path, overwrite: true);
        }
        finally { _writes.Release(); }
    }
}
