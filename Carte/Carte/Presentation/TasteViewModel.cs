using Carte.Core;
using Carte.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace Carte.Presentation;

public sealed partial class TasteViewModel : ObservableObject, IRecipient<SessionChangedMessage>
{
    private readonly MenuSession _session;
    private readonly INavigator _navigator;

    public TasteViewModel(MenuSession session, INavigator navigator)
    {
        _session = session;
        _navigator = navigator;
        WeakReferenceMessenger.Default.Register(this);
        LoadDraft();
        IsEditing = !session.TasteProfile.HasPreferences;
        Refresh();
        _ = session.EnsureLoadedAsync();
    }

    [ObservableProperty] public partial bool IsEditing { get; set; }
    [ObservableProperty] public partial int RichnessIndex { get; set; } = 1;
    [ObservableProperty] public partial int IntensityIndex { get; set; } = 1;
    [ObservableProperty] public partial int HeatIndex { get; set; } = 2;
    [ObservableProperty] public partial bool LovesZesty { get; set; }
    [ObservableProperty] public partial bool LovesHerbal { get; set; }
    [ObservableProperty] public partial bool LovesEarthy { get; set; }
    [ObservableProperty] public partial bool LovesCreamy { get; set; }
    [ObservableProperty] public partial string AvoidText { get; set; } = "";
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<TasteRecommendationViewModel> Recommendations { get; private set; } = [];

    public bool ShowResults => !IsEditing;
    public bool HasResults => Recommendations.Count > 0;
    public bool ShowEmpty => !HasResults;
    public bool HasFeedback => _session.TasteProfile.Feedback.Length > 0;
    public bool IsReady => _session.IsLoaded;
    public bool? Light { get => RichnessIndex == 0; set { if (value == true) RichnessIndex = 0; } }
    public bool? EitherWeight { get => RichnessIndex == 1; set { if (value == true) RichnessIndex = 1; } }
    public bool? Rich { get => RichnessIndex == 2; set { if (value == true) RichnessIndex = 2; } }
    public bool? Delicate { get => IntensityIndex == 0; set { if (value == true) IntensityIndex = 0; } }
    public bool? EitherIntensity { get => IntensityIndex == 1; set { if (value == true) IntensityIndex = 1; } }
    public bool? Bold { get => IntensityIndex == 2; set { if (value == true) IntensityIndex = 2; } }
    public bool? NoHeat { get => HeatIndex == 0; set { if (value == true) HeatIndex = 0; } }
    public bool? SomeHeat { get => HeatIndex == 1; set { if (value == true) HeatIndex = 1; } }
    public bool? AnyHeat { get => HeatIndex == 2; set { if (value == true) HeatIndex = 2; } }
    partial void OnRichnessIndexChanged(int value)
    { OnPropertyChanged(nameof(Light)); OnPropertyChanged(nameof(EitherWeight)); OnPropertyChanged(nameof(Rich)); }
    partial void OnIntensityIndexChanged(int value)
    { OnPropertyChanged(nameof(Delicate)); OnPropertyChanged(nameof(EitherIntensity)); OnPropertyChanged(nameof(Bold)); }
    partial void OnHeatIndexChanged(int value)
    { OnPropertyChanged(nameof(NoHeat)); OnPropertyChanged(nameof(SomeHeat)); OnPropertyChanged(nameof(AnyHeat)); }
    public string SaveMessage => _session.TasteSaveMessage;
    public string ResultsTitle => Recommendations.Count switch
    {
        0 => "Let’s find another direction.",
        1 => "One dish to start with.",
        2 => "Two dishes to start with.",
        _ => "Three dishes, chosen for you."
    };
    public string ProfileSummary
    {
        get
        {
            var p = _session.TasteProfile;
            var words = new List<string>();
            if (p.Richness is { } r) words.Add(r < .5 ? "Light" : "Rich");
            if (p.Intensity is { } i) words.Add(i < .5 ? "delicate" : "bold");
            words.AddRange(p.FavouriteFlavours);
            if (p.Heat == "none") words.Add("no chilli heat");
            if (p.Heat == "some") words.Add("a little heat");
            if (p.AvoidIngredients.Length > 0) words.Add("skip " + string.Join(", ", p.AvoidIngredients));
            return string.Join(" · ", words);
        }
    }

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowResults));
        StatusMessage = value ? "Edit your taste preferences." : ResultsTitle;
    }

    private void LoadDraft()
    {
        var p = _session.TasteProfile;
        RichnessIndex = p.Richness is null ? 1 : p.Richness < .5 ? 0 : 2;
        IntensityIndex = p.Intensity is null ? 1 : p.Intensity < .5 ? 0 : 2;
        HeatIndex = p.Heat == "none" ? 0 : p.Heat == "some" ? 1 : 2;
        LovesZesty = p.FavouriteFlavours.Contains("zesty");
        LovesHerbal = p.FavouriteFlavours.Contains("herbal");
        LovesEarthy = p.FavouriteFlavours.Contains("earthy");
        LovesCreamy = p.FavouriteFlavours.Contains("creamy");
        AvoidText = string.Join(", ", p.AvoidIngredients);
    }

    public void Receive(SessionChangedMessage message)
    {
        if (message.Change.HasFlag(SessionChange.Menu))
        {
            LoadDraft();
            IsEditing = !_session.TasteProfile.HasPreferences;
        }
        if ((message.Change & (SessionChange.Menu | SessionChange.Taste | SessionChange.Shortlist)) != 0) Refresh();
    }

    private void Refresh()
    {
        Recommendations = _session.Recommendations.Select((r, i) =>
            new TasteRecommendationViewModel(r, i + 1, _session, this)).ToArray();
        foreach (var property in new[] { nameof(HasResults), nameof(ShowEmpty), nameof(HasFeedback),
            nameof(ResultsTitle), nameof(ProfileSummary), nameof(SaveMessage), nameof(IsReady) })
            OnPropertyChanged(property);
    }

    [RelayCommand]
    private async Task FindDishes()
    {
        var flavours = new List<string>();
        if (LovesZesty) flavours.Add("zesty");
        if (LovesHerbal) flavours.Add("herbal");
        if (LovesEarthy) flavours.Add("earthy");
        if (LovesCreamy) flavours.Add("creamy");
        var profile = new TasteProfile(
            RichnessIndex == 1 ? null : RichnessIndex == 0 ? .2 : .8,
            IntensityIndex == 1 ? null : IntensityIndex == 0 ? .2 : .8,
            HeatIndex == 0 ? "none" : HeatIndex == 1 ? "some" : "any",
            flavours.ToArray(),
            AvoidText.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _session.TasteProfile.Feedback).Normalize();
        if (!profile.HasPreferences)
        {
            StatusMessage = "Choose at least one preference so we have something to go on.";
            return;
        }
        await _session.SetTasteAsync(profile);
        IsEditing = false;
    }

    [RelayCommand] private void Edit() { LoadDraft(); IsEditing = true; }
    [RelayCommand] private Task Back() => _navigator.ReturnToMenuAsync(this);
    [RelayCommand] private async Task ResetPreferences()
    {
        await _session.SetTasteAsync(TasteProfile.Empty);
        LoadDraft();
        IsEditing = true;
        StatusMessage = "Preferences reset. Start with what sounds good to you.";
    }
    [RelayCommand] private async Task ResetFeedback()
    {
        await _session.SetTasteAsync(_session.TasteProfile with { Feedback = [] });
        StatusMessage = "Dish feedback reset. Your taste preferences are unchanged.";
    }

    internal async Task FeedbackAsync(string id, bool likes)
    {
        var name = _session.Find(id)?.Name ?? "that dish";
        await _session.GiveTasteFeedbackAsync(id, likes);
        StatusMessage = likes ? $"We’ll look for more dishes like {name}." : $"We’ll suggest less like {name}.";
    }

    internal Task MapAsync(string id)
    {
        _session.Select(id);
        _session.SetView(ExploreView.Map);
        return _navigator.ReturnToMenuAsync(this);
    }
}

public sealed partial class TasteRecommendationViewModel(
    DishRecommendation recommendation, int rank, MenuSession session, TasteViewModel owner) : ObservableObject
{
    public string Id => recommendation.Item.Id;
    public string Name => recommendation.Item.Name;
    public string RankLabel => rank == 1 ? "Start here" : $"Another direction · 0{rank}";
    public string Reason => recommendation.Reason;
    public string Note => recommendation.Note;
    public bool HasNote => Note.Length > 0;
    public string Price => recommendation.Item.Price is { } price ? PriceFormatter.Format(price, session.Menu!.Locale) : "";
    public Microsoft.UI.Xaml.Media.ImageSource Glyph => Glyphs.Ink(recommendation.Item.Glyph);
    public string MapName => $"See {Name} on the taste map";
    public string MoreName => $"More like {Name}";
    public string LessName => $"Less like {Name}";
    public int AppearanceId => 1;
    public int AppearanceDelay => 250 + rank * 100;
    [RelayCommand] private Task ShowMap() => owner.MapAsync(Id);
    [RelayCommand] private Task More() => owner.FeedbackAsync(Id, true);
    [RelayCommand] private Task Less() => owner.FeedbackAsync(Id, false);
}
