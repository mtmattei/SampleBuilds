namespace Carte.Controls;

// Uppercases a TextBlock whenever its text is set, including by x:Uid. Used for DM Mono metadata labels.
public static class TextCase
{
    public static readonly DependencyProperty UpperProperty = DependencyProperty.RegisterAttached(
        "Upper", typeof(bool), typeof(TextCase), new PropertyMetadata(false, OnUpperChanged));

    public static bool GetUpper(TextBlock element) => (bool)element.GetValue(UpperProperty);
    public static void SetUpper(TextBlock element, bool value) => element.SetValue(UpperProperty, value);

    private static void OnUpperChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text || e.NewValue is not true) return;
        Apply(text);
        text.RegisterPropertyChangedCallback(TextBlock.TextProperty, (s, _) => Apply((TextBlock)s));
    }

    private static void Apply(TextBlock text)
    {
        var upper = text.Text?.ToUpper(System.Globalization.CultureInfo.CurrentCulture);
        if (upper != text.Text) text.Text = upper;
    }
}
