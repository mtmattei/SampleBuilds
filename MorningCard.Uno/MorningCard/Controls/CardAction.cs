namespace MorningCard.Controls;

/// <summary>
/// The two labels under an action's circle. Attached properties rather than a custom control: the
/// four actions differ only in their icon and their text, so a templated Button plus these carries
/// the whole design without a new control type.
/// </summary>
public static class CardAction
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.RegisterAttached(
            "Label",
            typeof(string),
            typeof(CardAction),
            new PropertyMetadata(string.Empty));

    /// <summary>The smaller second line, e.g. the cabin temperature under "Climate".</summary>
    public static readonly DependencyProperty SubLabelProperty =
        DependencyProperty.RegisterAttached(
            "SubLabel",
            typeof(string),
            typeof(CardAction),
            new PropertyMetadata(null));

    public static string GetLabel(DependencyObject element) => (string)element.GetValue(LabelProperty);

    public static void SetLabel(DependencyObject element, string value) => element.SetValue(LabelProperty, value);

    public static string? GetSubLabel(DependencyObject element) => (string?)element.GetValue(SubLabelProperty);

    public static void SetSubLabel(DependencyObject element, string? value) => element.SetValue(SubLabelProperty, value);
}
