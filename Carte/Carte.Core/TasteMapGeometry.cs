namespace Carte.Core;

public readonly record struct PlotPoint(double X, double Y);

public static class TasteMapGeometry
{
    // Semantic y grows upward; XAML screen y grows downward. Axis text stays outside this plot.
    public static PlotPoint ToPixel(double x, double y, double width, double height, double inset = 44)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(inset)
            || inset < 0 || width <= 2 * inset || height <= 2 * inset)
            throw new ArgumentOutOfRangeException(nameof(width), "The plot must be larger than its insets.");
        return new(inset + x * (width - 2 * inset), inset + (1 - y) * (height - 2 * inset));
    }

    public static void ValidateCoordinate(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(name, "Taste coordinates must be finite values between zero and one.");
    }

    // A collision flags a need for label offset, clustering or list fallback; do not move taste coordinates.
    public static bool HitTargetsOverlap(PlotPoint a, PlotPoint b, double targetSize = 44) =>
        Math.Abs(a.X - b.X) < targetSize && Math.Abs(a.Y - b.Y) < targetSize;
}
