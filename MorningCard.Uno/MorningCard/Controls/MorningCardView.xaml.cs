using MorningCard.Themes;

namespace MorningCard.Controls;

/// <summary>
/// The phone card: the shell composed from its parts (header, trip, map, checklist, footer). It
/// inherits the page's DataContext (the generated MainViewModel) and has no model of its own.
/// </summary>
public sealed partial class MorningCardView : UserControl
{
    public MorningCardView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // xaml-lint: allow codebehind - lifecycle: subscribes the Simulator radius dial, no XAML surface
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CardChrome.Changed += OnChromeChanged;
        ApplyChrome();
    }

    // xaml-lint: allow codebehind - lifecycle: releases the static CardChrome subscription
    private void OnUnloaded(object sender, RoutedEventArgs e) => CardChrome.Changed -= OnChromeChanged;

    // xaml-lint: allow codebehind - Simulator radius dial writes Glass.CornerRadius; a ThemeResource would not re-resolve
    private void OnChromeChanged(object? sender, EventArgs e) => ApplyChrome();

    /// <summary>
    /// The radius dial writes here rather than into a theme resource: a ThemeResource only
    /// re-resolves on a theme change, so an already-bound CornerRadius would never move.
    /// </summary>
    private void ApplyChrome()
    {
        var radius = CardChrome.CornerRadius;
        Glass.CornerRadius = new CornerRadius(radius);
    }
}
