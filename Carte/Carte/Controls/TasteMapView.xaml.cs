using Carte.Core;
using Carte.Presentation;
using Microsoft.UI.Xaml.Automation;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Carte.Controls;

// Quadrant view of one category. Code-behind does visual layout only: it converts normalized
// coordinates to pixels after measurement and on resize, and reports selection through SelectedItem.
public sealed partial class TasteMapView : UserControl
{
    private const double Inset = 52;
    private const double Target = 44;
    private const double AxisGap = 8;

    private readonly List<Mark> _marks = [];
    private readonly bool _animate = new UISettings().AnimationsEnabled;

    public TasteMapView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        Plot.SizeChanged += (_, _) => Arrange();
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IReadOnlyList<MenuItemViewModel>), typeof(TasteMapView),
        new PropertyMetadata(null, (d, _) => ((TasteMapView)d).Rebuild()));

    public static readonly DependencyProperty AxisSetProperty = DependencyProperty.Register(
        nameof(AxisSet), typeof(AxisSet), typeof(TasteMapView),
        new PropertyMetadata(null, (d, _) => ((TasteMapView)d).UpdateAxes()));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(MenuItemViewModel), typeof(TasteMapView),
        new PropertyMetadata(null, (d, _) => ((TasteMapView)d).UpdateStates(useTransitions: true)));

    public IReadOnlyList<MenuItemViewModel>? ItemsSource
    {
        get => (IReadOnlyList<MenuItemViewModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public AxisSet? AxisSet
    {
        get => (AxisSet?)GetValue(AxisSetProperty);
        set => SetValue(AxisSetProperty, value);
    }

    public MenuItemViewModel? SelectedItem
    {
        get => (MenuItemViewModel?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    // The label sits on a paper backing so axis rules never run through its text.
    private sealed record Mark(MenuItemViewModel Item, Button Glyph, Border Label, TextBlock Text);

    // A near-square plot that grows with the column; reflow happens in Arrange.
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var height = Math.Clamp(e.NewSize.Width * 0.86, 260, 520);
        if (Math.Abs(Plot.Height - height) > 0.5) Plot.Height = height;
    }

    private void UpdateAxes()
    {
        var axes = AxisSet;
        TopLabel.Text = axes?.Y.High ?? "";
        BottomLabel.Text = axes?.Y.Low ?? "";
        LeftLabel.Text = axes?.X.Low ?? "";
        RightLabel.Text = axes?.X.High ?? "";
        AutomationProperties.SetName(Root, axes is null ? "" : Loc.Format("Format.TasteMapName", TasteDescriber.DescribeAxes(axes)));
        Arrange();
    }

    private void Rebuild()
    {
        foreach (var mark in _marks)
        {
            Surface.Children.Remove(mark.Label);
            Surface.Children.Remove(mark.Glyph);
        }
        _marks.Clear();

        // Unplaced items stay out of the plot; the page lists them beneath the map.
        foreach (var item in ItemsSource?.Where(i => i.IsPlaced) ?? [])
        {
            var glyph = new Button { Style = (Style)Application.Current.Resources["CarteGlyphButtonStyle"], DataContext = item };
            AutomationProperties.SetName(glyph, item.AccessibleName);
            glyph.Click += (_, _) => SelectedItem = item;
            glyph.Loaded += (_, _) => UpdateStates(useTransitions: false);

            var text = new TextBlock { Text = item.ShortName, Style = (Style)Application.Current.Resources["CarteMapLabelTextStyle"] };
            var label = new Border
            {
                Child = text,
                Padding = new Thickness(3, 0, 3, 0),
                Background = (Brush)Application.Current.Resources["CartePaperBrush"],
                IsHitTestVisible = false
            };
            AutomationProperties.SetAccessibilityView(label, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);

            // Children follow menu order, so Tab order matches the menu list.
            Surface.Children.Add(label);
            Surface.Children.Add(glyph);
            _marks.Add(new Mark(item, glyph, label, text));
        }
        UpdateStates(useTransitions: false);
    }

    private void UpdateStates(bool useTransitions)
    {
        foreach (var (item, glyph, _, text) in _marks)
        {
            var selected = ReferenceEquals(item, SelectedItem);
            VisualStateManager.GoToState(glyph, selected ? "Selected" : "Unselected", useTransitions && _animate);
            VisualStateManager.GoToState(glyph, item.IsLowConfidence ? "LowConfidence" : "Confident", false);
            text.FontFamily = (FontFamily)Application.Current.Resources[selected ? "CarteBodySemiBoldFont" : "CarteBodyFont"];
        }
        Arrange();
    }

    private void Arrange()
    {
        var (width, height) = (Plot.ActualWidth, Plot.ActualHeight);
        if (width <= 2 * Inset || height <= 2 * Inset) return;

        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        LeftLabel.Measure(infinite);
        RightLabel.Measure(infinite);
        var left = new PlotRect(0, height / 2 - LeftLabel.DesiredSize.Height / 2, LeftLabel.DesiredSize.Width, LeftLabel.DesiredSize.Height);
        var right = new PlotRect(width - RightLabel.DesiredSize.Width, height / 2 - RightLabel.DesiredSize.Height / 2,
            RightLabel.DesiredSize.Width, RightLabel.DesiredSize.Height);
        Place(LeftLabel, left.X, left.Y);
        Place(RightLabel, right.X, right.Y);

        VerticalRule.Height = height;
        Place(VerticalRule, width / 2, 0);
        HorizontalRule.Width = Math.Max(0, right.X - left.Right - 2 * AxisGap);
        Place(HorizontalRule, left.Right + AxisGap, height / 2);
        Place(CentreDot, width / 2 - 2.5, height / 2 - 2.5);

        var inputs = _marks.Select(m =>
        {
            m.Label.Measure(infinite);
            var centre = TasteMapGeometry.ToPixel(m.Item.X, m.Item.Y, width, height, Inset);
            return new MapLabelInput(m.Item.Id, centre, m.Label.DesiredSize.Width, m.Label.DesiredSize.Height);
        }).ToArray();
        var layout = MapLabelLayout.Arrange(inputs, width, height, [left, right], Target);

        for (var i = 0; i < _marks.Count; i++)
        {
            Place(_marks[i].Glyph, inputs[i].Centre.X - Target / 2, inputs[i].Centre.Y - Target / 2);
            Place(_marks[i].Label, layout.Labels[i].Label.X, layout.Labels[i].Label.Y);
        }
        CrowdedNote.Visibility = layout.HasCrowdedTargets ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
    }
}
