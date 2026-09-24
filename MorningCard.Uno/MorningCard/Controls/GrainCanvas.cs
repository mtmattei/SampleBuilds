using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace MorningCard.Controls;

/// <summary>
/// Film grain over the slate ground: a small noise tile, generated once, repeated across the area.
///
/// The tile is monochrome and mixes faint white and faint black specks, so the grain reads as
/// texture rather than as a lighter or darker ground. Its alpha is baked into the pixels because
/// <c>UIElement.Opacity</c> does not reach SKCanvasElement drawing. Nothing is animated and nothing
/// is allocated per frame: the tile and its repeat shader are built in the constructor.
/// </summary>
public sealed partial class GrainCanvas : SKCanvasElement
{
    /// <summary>Tile edge, in effective pixels. Large enough that the repeat is not visible.</summary>
    private const int TileSize = 128;

    /// <summary>The strongest speck, out of 255. Low: the grain should be felt more than seen.</summary>
    private const int MaxAlpha = 22;

    /// <summary>Fixed, so the texture is the same on every launch and in every capture.</summary>
    private const int Seed = 7;

    private readonly SKBitmap _tile;
    private readonly SKShader _shader;
    private readonly SKPaint _paint;

    public GrainCanvas()
    {
        IsHitTestVisible = false;

        _tile = new SKBitmap(new SKImageInfo(TileSize, TileSize, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var random = new Random(Seed);
        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                var alpha = (byte)random.Next(0, MaxAlpha + 1);
                var ink = random.Next(2) == 0 ? (byte)0 : (byte)255;
                _tile.SetPixel(x, y, new SKColor(ink, ink, ink, alpha));
            }
        }

        _shader = SKShader.CreateBitmap(_tile, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        _paint = new SKPaint { Shader = _shader, IsAntialias = false };
    }

    protected override void RenderOverride(SKCanvas canvas, Size area) =>
        canvas.DrawRect(0, 0, (float)area.Width, (float)area.Height, _paint);
}
