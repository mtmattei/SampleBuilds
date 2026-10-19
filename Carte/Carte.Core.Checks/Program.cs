using Carte.Core;

var count = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
void Reject(Action action, string name)
{
    try { action(); } catch (ArgumentException) { count++; return; }
    throw new Exception(name);
}
var menu = MenuDocument.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "menu.sample.json")));
Check(menu.Items.Count == 18 && menu.Categories.Count == 3, "Sample data loaded");
Check(MenuDocument.FromJson(menu.ToJson()).Items.Count == 18, "Round trip");
Check(TasteMapGeometry.ToPixel(0, 1, 300, 300) == new PlotPoint(44, 44), "Top left semantic direction");
Check(TasteMapGeometry.ToPixel(1, 0, 300, 300) == new PlotPoint(256, 256), "Bottom right semantic direction");
Check(TasteMapGeometry.ToPixel(.5, .5, 400, 300) == new PlotPoint(200, 150), "Responsive centre");
Reject(() => TasteMapGeometry.ToPixel(double.NaN, .5, 300, 300), "Reject NaN");
Reject(() => TasteMapGeometry.ToPixel(1.1, .5, 300, 300), "Reject out of range");
Reject(() => TasteMapGeometry.ToPixel(.5, .5, 80, 80), "Reject invalid plot");
Check(TasteMapGeometry.HitTargetsOverlap(new(100, 100), new(120, 120)), "Detect touch collision");
Check(!TasteMapGeometry.HitTargetsOverlap(new(100, 100), new(144, 100)), "Separate touch targets");
var original = menu.Items[0];
var stale = original with { Placement = original.Placement! with { AxisRevision = 99 } };
Reject(() => MenuValidation.Validate(menu with { Items = [stale, .. menu.Items.Skip(1)] }), "Reject stale axes");
Reject(() => MenuValidation.Validate(menu with { Items = [original, .. menu.Items] }), "Reject duplicate IDs");
var unknown = original with { Placement = null, Price = null };
var missingPriceMenu = menu with { Items = [unknown, .. menu.Items.Skip(1)] };
MenuValidation.Validate(missingPriceMenu); count++;
var shortlist = new TonightShortlist();
Check(shortlist.Toggle("crudo", menu), "Save item");
Check(shortlist.GetSubtotal(menu).KnownAmount == 19, "Subtotal");
Check(!shortlist.Toggle("crudo", menu) && shortlist.ItemIds.Count == 0, "Remove item");
shortlist.Restore(["crudo", "crudo", "missing"], menu);
Check(shortlist.ItemIds.Count == 1, "Restore deduplicates and ignores stale IDs");
Check(shortlist.GetSubtotal(missingPriceMenu).MissingPriceCount == 1, "Missing price is not silently zero");
Reject(() => shortlist.Toggle("missing", menu), "Reject unknown item");

// App-level additions: taste words, prices, label layout and the file store.
var plates = menu.Categories.Single(c => c.Id == "plates").AxisSet;
Check(TasteDescriber.Describe(original.Placement!, plates) == "light and bold", "Describe corner placement");
var tomatoes = menu.Items.Single(i => i.Id == "tomatoes");
Check(TasteDescriber.Describe(tomatoes.Placement!, plates) == "between light and rich, bold", "Describe middle band");
Check(PriceFormatter.Format(original.Price!, "en-US") == "$19", "Local currency symbol");
Check(PriceFormatter.Format(menu.Items.Single(i => i.Id == "albarino").Price!, "en-US") == "$13 / glass", "Serving unit kept");
Check(PriceFormatter.FormatAmount(19, "EUR", "en-US") == "19 EUR", "Foreign currency uses ISO code");
Check(PriceFormatter.FormatAmount(12.5m, "USD", "en-US") == "$12.50", "Fractional price");
var close = MapLabelLayout.Arrange(
    [new("a", new(150, 150), 60, 16), new("b", new(150, 190), 60, 16)], 300, 300, []);
Check(close.HasCrowdedTargets, "Report crowded targets");
Check(!close.Labels[0].Label.Intersects(close.Labels[1].Label), "Labels do not overlap");
Check(close.Labels.All(l => !l.Label.Intersects(PlotRect.Around(new(150, 150), 44))
    && !l.Label.Intersects(PlotRect.Around(new(150, 190), 44))), "Labels avoid glyph targets");
var apart = MapLabelLayout.Arrange([new("a", new(60, 60), 60, 16), new("b", new(240, 240), 60, 16)], 300, 300, []);
Check(!apart.HasCrowdedTargets && apart.Labels[0].Label.Y > 60, "Default label sits below its glyph");

var root = Path.Combine(Path.GetTempPath(), "carte-checks-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new FileMenuStore(root);
    var none = CancellationToken.None;
    Check(await store.LoadMenuAsync(menu.Id, none) is null, "Missing menu loads as null");
    Check((await store.LoadShortlistAsync(menu.Id, none)).Count == 0, "Missing shortlist is empty");
    await store.SaveMenuAsync(menu, none);
    await store.SaveShortlistAsync(menu.Id, ["crudo", "albarino"], none);
    Check((await store.LoadMenuAsync(menu.Id, none))?.Items.Count == 18, "Menu survives restart");
    Check((await store.LoadShortlistAsync(menu.Id, none)).SequenceEqual(["crudo", "albarino"]), "Shortlist survives restart");
    Check((await store.LoadShortlistAsync("other-menu", none)).Count == 0, "Shortlist is scoped to menu ID");
    File.WriteAllText(Path.Combine(root, "menus", menu.Id + ".json"), "{ not json");
    Check(await store.LoadMenuAsync(menu.Id, none) is null, "Corrupt menu loads as null");
    Reject(() => store.LoadMenuAsync("../escape", none).GetAwaiter().GetResult(), "Reject unsafe menu ID");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}
Console.WriteLine($"PASS: {count} domain checks.");
