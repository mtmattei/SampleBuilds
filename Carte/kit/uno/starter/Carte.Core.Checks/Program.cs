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
Console.WriteLine($"PASS: {count} domain checks.");
