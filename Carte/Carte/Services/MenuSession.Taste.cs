using Carte.Core;

namespace Carte.Services;

public sealed partial class MenuSession
{
    private FileTasteProfileStore? _tasteStore;
    public TasteProfile TasteProfile { get; private set; } = TasteProfile.Empty;
    public string TasteSaveMessage { get; private set; } = "";
    public IReadOnlyList<DishRecommendation> Recommendations =>
        Menu is { } menu ? TasteRecommendations.Recommend(menu, TasteProfile) : [];

    private async Task LoadTasteAsync(string folder)
    {
        _tasteStore = new FileTasteProfileStore(folder);
#if DEBUG
        // Recording preferences are temporary; never replace the diner's saved choices.
        if (IsTastePreview) return;
#endif
        try { TasteProfile = await _tasteStore.LoadAsync(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not load taste preferences.");
            TasteSaveMessage = "Your saved preferences could not be loaded. You can make new choices here.";
        }
    }

    public async Task SetTasteAsync(TasteProfile profile)
    {
        TasteProfile = profile.Normalize();
        TasteSaveMessage = "";
        Notify(SessionChange.Taste);
#if DEBUG
        if (IsTastePreview) return;
#endif
        try
        {
            if (_tasteStore is not null) await _tasteStore.SaveAsync(TasteProfile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not save taste preferences.");
            TasteSaveMessage = "These choices work for this visit, but could not be saved. Try saving them again.";
            Notify(SessionChange.Taste);
        }
    }

    public Task GiveTasteFeedbackAsync(string itemId, bool likes)
    {
        if (Menu is null || Find(itemId) is null) return Task.CompletedTask;
        var feedback = TasteProfile.Feedback.Where(f => f.MenuId != Menu.Id || f.ItemId != itemId)
            .Append(new TasteFeedback(Menu.Id, itemId, likes)).ToArray();
        return SetTasteAsync(TasteProfile with { Feedback = feedback });
    }

#if DEBUG
    private static bool IsTastePreview => Environment.GetCommandLineArgs()
        .Any(arg => arg is "--taste-tour" or "--taste-preview");
#endif
}
