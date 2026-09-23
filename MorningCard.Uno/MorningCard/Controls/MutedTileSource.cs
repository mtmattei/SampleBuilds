using BruTile;
using SkiaSharp;

namespace MorningCard.Controls;

/// <summary>
/// Wraps an HTTP tile source and re-renders every tile once through a colour matrix, so OSM tiles
/// arrive already muted toward the card's paper ground.
///
/// The tint lives at the tile, not at the renderer: Mapsui caches the tinted bytes like any other
/// tile, nothing extra runs per frame, and it does not depend on Mapsui's renderer internals.
/// A second matrix (the car's night variant) is a second instance of this class.
/// </summary>
internal sealed class MutedTileSource(IHttpTileSource inner, float[] colorMatrix) : IHttpTileSource
{
    private readonly SKColorFilter _filter = SKColorFilter.CreateColorMatrix(colorMatrix);

    /// <summary>
    /// Desaturates to luma, then scales the paper colour by it: the OSM land tone (luma ~0.95)
    /// lands on the paper colour itself, roads go slightly lighter, water and parks go a step
    /// darker, and label ink sinks to a warm mid-grey.
    /// </summary>
    public static float[] PaperMatrix(SKColor paper, float floor = 0.35f, float gain = 0.68f)
    {
        float[] row(float channel)
        {
            var c = channel / 255f;
            return [c * gain * 0.2126f, c * gain * 0.7152f, c * gain * 0.0722f, 0, c * floor];
        }

        return [.. row(paper.Red), .. row(paper.Green), .. row(paper.Blue), 0, 0, 0, 1, 0];
    }

    public ITileSchema Schema => inner.Schema;

    public string Name => inner.Name;

    public Attribution Attribution => inner.Attribution;

    public async Task<byte[]?> GetTileAsync(HttpClient httpClient, TileInfo tileInfo, CancellationToken? cancellationToken = null)
    {
        var bytes = await inner.GetTileAsync(httpClient, tileInfo, cancellationToken);

        return bytes is null ? null : Tint(bytes);
    }

    private byte[] Tint(byte[] png)
    {
        using var source = SKBitmap.Decode(png);

        if (source is null)
        {
            return png;
        }

        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height));
        using var paint = new SKPaint { ColorFilter = _filter };

        surface.Canvas.DrawBitmap(source, 0, 0, paint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}
