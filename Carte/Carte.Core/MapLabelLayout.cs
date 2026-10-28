namespace Carte.Core;

public readonly record struct PlotRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Intersects(PlotRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public double OverlapArea(PlotRect other) =>
        Math.Max(0, Math.Min(Right, other.Right) - Math.Max(X, other.X))
        * Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y));

    public static PlotRect Around(PlotPoint centre, double size) =>
        new(centre.X - size / 2, centre.Y - size / 2, size, size);
}

public sealed record MapLabelInput(string Id, PlotPoint Centre, double LabelWidth, double LabelHeight);
public sealed record MapLabelPlacement(string Id, PlotRect Label);
public sealed record MapLabelResult(IReadOnlyList<MapLabelPlacement> Labels, bool HasCrowdedTargets);

// Places item labels around fixed glyph centres. Only labels move; taste coordinates never do.
// Crowded hit targets are reported so the UI can point to the list view.
public static class MapLabelLayout
{
    private const double Gap = 6;

    public static MapLabelResult Arrange(IReadOnlyList<MapLabelInput> items, double width, double height,
        IReadOnlyList<PlotRect> obstacles, double targetSize = 44)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(obstacles);
        var bounds = new PlotRect(0, 0, width, height);
        var targets = items.Select(i => PlotRect.Around(i.Centre, targetSize)).ToArray();
        var crowded = false;
        for (var a = 0; a < items.Count && !crowded; a++)
            for (var b = a + 1; b < items.Count && !crowded; b++)
                crowded = TasteMapGeometry.HitTargetsOverlap(items[a].Centre, items[b].Centre, targetSize);

        var taken = new List<PlotRect>(targets.Concat(obstacles));
        var placed = new Dictionary<string, PlotRect>(StringComparer.Ordinal);
        foreach (var item in items.OrderBy(i => i.Centre.Y).ThenBy(i => i.Centre.X))
        {
            PlotRect? best = null;
            var bestCost = double.MaxValue;
            foreach (var candidate in Candidates(item, targetSize, width))
            {
                // Labels inside the plot win; overlap with glyphs, labels and axis text costs its area.
                var cost = taken.Sum(t => t.OverlapArea(candidate))
                    + (candidate.Width * candidate.Height - candidate.OverlapArea(bounds)) * 4;
                if (cost < bestCost) { best = candidate; bestCost = cost; }
                if (cost == 0) break;
            }
            placed[item.Id] = best!.Value;
            taken.Add(best.Value);
        }
        return new(items.Select(i => new MapLabelPlacement(i.Id, placed[i.Id])).ToArray(), crowded);
    }

    // Preference order: below, above, then the side facing the plot centre, then the other side.
    private static IEnumerable<PlotRect> Candidates(MapLabelInput item, double targetSize, double plotWidth)
    {
        var (cx, cy, w, h, half) = (item.Centre.X, item.Centre.Y, item.LabelWidth, item.LabelHeight, targetSize / 2);
        yield return new(cx - w / 2, cy + half + Gap, w, h);
        yield return new(cx - w / 2, cy - half - Gap - h, w, h);
        var right = new PlotRect(cx + half + Gap, cy - h / 2, w, h);
        var left = new PlotRect(cx - half - Gap - w, cy - h / 2, w, h);
        var facesRight = cx < plotWidth / 2;
        yield return facesRight ? right : left;
        yield return facesRight ? left : right;
    }
}
