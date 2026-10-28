using System.Text.RegularExpressions;

namespace Carte.Core;

public sealed record TasteFeedback(string MenuId, string ItemId, bool Likes);

public sealed record TasteProfile(
    double? Richness, double? Intensity, string Heat,
    string[] FavouriteFlavours, string[] AvoidIngredients, TasteFeedback[] Feedback)
{
    public static TasteProfile Empty => new(null, null, "any", [], [], []);
    public bool HasPreferences => Richness is not null || Intensity is not null || Heat != "any"
        || FavouriteFlavours.Length > 0 || AvoidIngredients.Length > 0 || Feedback.Length > 0;

    public TasteProfile Normalize() => new(
        ValidAxis(Richness), ValidAxis(Intensity),
        Heat is "none" or "some" ? Heat : "any",
        (FavouriteFlavours ?? []).Where(x => x is "zesty" or "herbal" or "earthy" or "creamy").Distinct().ToArray(),
        (AvoidIngredients ?? []).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant()).Distinct().Take(30).ToArray(),
        (Feedback ?? []).Where(x => x is not null && !string.IsNullOrWhiteSpace(x.MenuId) && !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => (x.MenuId, x.ItemId)).Select(x => x.Last()).TakeLast(100).ToArray());

    private static double? ValidAxis(double? value) =>
        value is { } v && double.IsFinite(v) && v >= 0 && v <= 1 ? v : null;
}

public sealed record DishRecommendation(MenuItem Item, double Score, string Reason, string Note);

/// <summary>
/// Transparent, local ranking from this menu's calibrated food axes and written ingredients.
/// Scores are ordering weights, never a probability of enjoyment. Drinks use different axes.
/// </summary>
public static partial class TasteRecommendations
{
    private static readonly Dictionary<string, string[]> FlavourWords = new()
    {
        ["zesty"] = ["lime", "lemon", "vinegar", "citrus", "zesty", "tangy"],
        ["herbal"] = ["basil", "sage", "coriander", "parsley", "dill", "herbal"],
        ["earthy"] = ["mushroom", "truffle", "earthy"],
        ["creamy"] = ["butter", "burrata", "cream", "creamy", "buttery"]
    };

    public static IReadOnlyList<DishRecommendation> Recommend(MenuDocument menu, TasteProfile input, int count = 3)
    {
        var profile = input.Normalize();
        if (!profile.HasPreferences || count <= 0) return [];
        var foodCategories = menu.Categories.Where(c =>
            c.AxisSet.X.Id == "richness" && c.AxisSet.Y.Id == "intensity").Select(c => c.Id).ToHashSet();
        var feedback = profile.Feedback.Where(f => f.MenuId == menu.Id).ToArray();
        var liked = feedback.Where(f => f.Likes).Select(f => menu.Items.FirstOrDefault(i => i.Id == f.ItemId))
            .OfType<MenuItem>().Where(i => foodCategories.Contains(i.CategoryId)).ToArray();
        var disliked = feedback.Where(f => !f.Likes).Select(f => menu.Items.FirstOrDefault(i => i.Id == f.ItemId))
            .OfType<MenuItem>().Where(i => foodCategories.Contains(i.CategoryId)).ToArray();
        var ranked = new List<DishRecommendation>();

        foreach (var item in menu.Items.Where(i => foodCategories.Contains(i.CategoryId)))
        {
            if (disliked.Any(i => i.Id == item.Id) || profile.AvoidIngredients.Any(term => Mentions(item, term)))
                continue;
            var mentionsHeat = new[] { "chilli", "chili", "jalapeno", "jalapeño", "hot sauce", "spicy", "harissa" }
                .Any(term => Mentions(item, term));
            if (profile.Heat == "none" && mentionsHeat) continue;
            var reasons = new List<string>();
            var score = 0d;
            var evidence = 0;
            var notes = new List<string>();
            if (item.Placement is { } p)
            {
                if (profile.Richness is { } richness)
                {
                    score += 3 * (1 - Math.Abs(p.X - richness));
                    evidence++;
                    if (Math.Abs(p.X - richness) <= .3)
                        reasons.Add(richness < .5
                            ? "You prefer lighter dishes, and this sits toward the light side of this menu."
                            : "You enjoy richness, and this sits toward the rich side of this menu.");
                }
                if (profile.Intensity is { } intensity)
                {
                    score += 2.5 * (1 - Math.Abs(p.Y - intensity));
                    evidence++;
                    if (Math.Abs(p.Y - intensity) <= .3)
                        reasons.Add(intensity < .5
                            ? "Its delicate flavour suits your preference for gentler dishes."
                            : "Its bold flavour fits the intensity you enjoy.");
                }
                if (p.Confidence == "low") notes.Add("Taste placement is uncertain.");
            }
            else notes.Add("This dish has not been placed on the taste map yet.");

            foreach (var flavour in profile.FavouriteFlavours)
            {
                var evidenceWord = FlavourWords[flavour].FirstOrDefault(word => Mentions(item, word));
                if (evidenceWord is null) continue;
                score += 2;
                evidence++;
                reasons.Insert(0, $"You like {flavour} flavours; the menu mentions {evidenceWord}.");
            }
            if (profile.Heat == "some" && mentionsHeat)
            {
                score += 1;
                evidence++;
                reasons.Add("You welcome a little heat, and the menu mentions chilli or spice.");
            }
            // Feedback is comparable only within the same food axis set.
            var similar = liked.Where(i => i.Id != item.Id && i.Placement is not null && item.Placement is not null
                    && i.Placement.AxisSetId == item.Placement.AxisSetId
                    && i.Placement.AxisRevision == item.Placement.AxisRevision)
                .Select(i => (Item: i, Similarity: Similarity(item, i))).OrderByDescending(x => x.Similarity).FirstOrDefault();
            if (similar.Item is not null)
            {
                score += 2 * similar.Similarity;
                if (similar.Similarity >= .7)
                {
                    evidence++;
                    reasons.Insert(0, $"Like {similar.Item.Name}, which you liked, this has a similar balance of richness and intensity.");
                }
            }
            if (liked.Any(i => i.Id == item.Id))
            {
                score += 2;
                evidence++;
                reasons.Insert(0, "You asked for more like this dish.");
            }
            score -= disliked.Where(i => i.Placement is not null && item.Placement is not null
                && i.Placement.AxisSetId == item.Placement.AxisSetId && i.Placement.AxisRevision == item.Placement.AxisRevision)
                .Sum(i => 1.5 * Similarity(item, i));

            if (evidence == 0 && profile.AvoidIngredients.Length == 0 && profile.Heat == "any") continue;
            if (reasons.Count == 0)
                reasons.Add(evidence > 0
                    ? "An alternative to your usual taste; it is further from the balance you chose."
                    : "An option after applying your ingredient and heat choices; there is not enough detail for a flavour match.");
            if (mentionsHeat && profile.Heat != "none") notes.Add("The menu mentions heat; the amount is not specified.");
            ranked.Add(new(item, score, string.Join(" ", reasons.Take(2)), string.Join(" ", notes)));
        }
        return ranked.OrderByDescending(r => r.Score).ThenBy(r => r.Item.Id, StringComparer.Ordinal).Take(count).ToArray();
    }

    private static double Similarity(MenuItem a, MenuItem b) =>
        a.Placement is { } x && b.Placement is { } y
            ? 1 - (Math.Abs(x.X - y.X) + Math.Abs(x.Y - y.Y)) / 2 : 0;

    public static bool Mentions(MenuItem item, string term)
    {
        var text = NormalizeWords($"{item.Name} {item.OriginalMenuText} {item.Description} {string.Join(" ", item.SensoryTags)}");
        var query = NormalizeWords(term);
        var aliases = query switch
        {
            "cilantro" or "coriander" => new[] { "cilantro", "coriander" },
            "chili" or "chilli" => new[] { "chili", "chilli" },
            "beef" or "steak" => new[] { "beef", "steak", "bavette" },
            "fish" => new[] { "fish", "sea bass", "salmon", "tuna", "cod", "anchovy" },
            "shellfish" => new[] { "oyster", "prawn", "shrimp", "crab", "lobster", "mussel", "clam", "scallop" },
            "dairy" => new[] { "milk", "cream", "butter", "burrata", "cheese", "yogurt", "yoghurt" },
            _ => [query]
        };
        return aliases.Any(word => word.Length > 0 && (" " + text + " ").Contains(" " + word + " ", StringComparison.Ordinal));
    }

    private static string NormalizeWords(string text) => string.Join(" ",
        Words().Matches(text.ToLowerInvariant()).Select(m => m.Value switch
        {
            "mushrooms" => "mushroom", "tomatoes" => "tomato", "oysters" => "oyster",
            "peaches" => "peach", "chillies" => "chilli", "chilies" => "chili",
            "peppercorns" => "peppercorn", "nuts" => "nut", _ => m.Value
        }));

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();
}
