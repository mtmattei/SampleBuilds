using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Shapes;
using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Presentation;
using MorningCard.Themes;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Microsoft.UI;
using Windows.UI;

namespace MorningCard.Controls;

/// <summary>
/// The v6 slack ruler: a 36-minute window with one tick per minute, the day's blocks (past, slack,
/// drive, walk, after, overrun), the key times above and the now marker.
///
/// Built in code on a Canvas because every position is a minute times the width, and no platform
/// or Toolkit control lays out a proportional timeline. The geometry is <see cref="SlackRulerLayout"/>
/// in Core (unit-tested); this only maps minutes to pixels, picks colours, and fits labels.
/// Rebuilt when the view or the width changes - a few dozen elements, a few times a minute.
///
/// Vertical metrics are the phone artboard's (104 epx): key times at 0, ticks at 46, blocks 56-86,
/// drop lines 40-90, now dot at 41, now caption at 91.
/// </summary>
public sealed partial class SlackRuler : Canvas
{
    public static readonly DependencyProperty ViewProperty = DependencyProperty.Register(
        nameof(View), typeof(EtaView), typeof(SlackRuler), new PropertyMetadata(null, (d, _) => ((SlackRuler)d).Rebuild()));

    private const double BlockTop = 56;
    private const double BlockHeight = 30;
    private const double BlockRadius = 6;
    private const double TickTop = 46;
    private const double LineTop = 40;
    private const double LineHeight = 50;
    private const double KeyGap = 6;

    public SlackRuler()
    {
        Height = 104;
        SizeChanged += (_, e) =>
        {
            if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5)
            {
                Rebuild();
            }
        };
    }

    public EtaView? View
    {
        get => (EtaView?)GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    private void Rebuild()
    {
        Children.Clear();

        var width = ActualWidth;

        if (View is not { Slack: { } plan } view || width <= 0)
        {
            AutomationProperties.SetName(this, string.Empty);
            return;
        }

        AutomationProperties.SetName(this, view.RulerDescription);

        double X(int minute) => minute / (double)plan.WindowMinutes * width;

        var tone = ToneColor(view.Tone);
        var toneInk = ToneInkColor(view.Tone);

        // Ticks: one per minute, taller and darker every five.
        var ink = Token("InkColor");
        for (var m = 0; m <= plan.WindowMinutes; m++)
        {
            var major = m % 5 == 0;
            Place(new Rectangle
            {
                Width = 1,
                Height = major ? 7 : 4,
                RadiusX = 0.5,
                RadiusY = 0.5,
                Fill = Brush(WithAlpha(ink, major ? 0.28 : 0.13)),
            }, X(m) - 0.5, major ? TickTop : TickTop + 3);
        }

        // Blocks, inset a pixel each side so neighbours read as separate pieces.
        foreach (var segment in plan.Segments.Where(s => s.Kind != RulerSegmentKind.Overrun))
        {
            var left = X(segment.StartMin) + 1;
            var blockWidth = Math.Max(0, X(segment.EndMin) - X(segment.StartMin) - 2);
            Place(Block(segment, view, blockWidth), left, BlockTop);
        }

        if (plan.Find(RulerSegmentKind.Overrun) is { } overrun)
        {
            var left = X(overrun.StartMin) + 1;
            var blockWidth = Math.Max(0, X(overrun.EndMin) - X(overrun.StartMin) - 2);
            Place(Hatch(blockWidth, Colors.Transparent, Token("OverrunColor"), 1.5f, 5f, Token("OverrunRingColor")), left, BlockTop);
        }

        // Drop lines from the key times through the blocks.
        if (plan.Leave.ShowLine)
        {
            Place(Line(1, WithAlpha(Token("AccentColor"), 0.55)), X(plan.Leave.AtMin) - 0.5, LineTop);
        }

        Place(Line(1, WithAlpha(ink, 0.35)), X(plan.Arrive.AtMin) - 0.5, LineTop);
        Place(Line(1.5, ink), X(plan.Bell.AtMin) - 0.75, LineTop);

        // Now: a line in the tone, and a dot on a panel-coloured ring unless leaving is now, where
        // the line takes the leave line's place instead.
        var nowX = X(plan.NowMin);
        Place(new Rectangle
        {
            Width = 1.5,
            Height = plan.LeaveIsNow ? LineHeight : 44,
            RadiusX = 0.75,
            RadiusY = 0.75,
            Fill = Brush(tone),
        }, nowX - 0.75, plan.LeaveIsNow ? LineTop : TickTop);

        if (!plan.LeaveIsNow)
        {
            Place(new Ellipse { Width = 13, Height = 13, Fill = Brush(Token("CardSurfaceColor")) }, nowX - 6.5, 39);
            Place(new Ellipse { Width = 9, Height = 9, Fill = Brush(tone) }, nowX - 4.5, 41);
            PlaceCentered(Text(view.NowCaption, "TimelineLabel", toneInk), nowX, 91, width);
        }

        // Key times.
        PlaceKey(view.LeaveCaption, view.LeaveValue, toneInk, plan.Leave, X(plan.Leave.AtMin), width);
        PlaceKey(view.ArriveCaption, view.ArriveTime, view.IsLate ? Token("DangerInkColor") : ink, plan.Arrive, X(plan.Arrive.AtMin), width);
        PlaceKey(view.BellCaption, view.BellTime, ink, plan.Bell, X(plan.Bell.AtMin), width);
    }

    private FrameworkElement Block(RulerSegment segment, EtaView view, double width)
    {
        switch (segment.Kind)
        {
            case RulerSegmentKind.Past:
            case RulerSegmentKind.After:
                return Hatch(width, Token("HatchGroundColor"), Token("HatchColor"), 1f, 6f, null);
        }

        var (fill, textInk, full, shortLabel, padding) = segment.Kind switch
        {
            RulerSegmentKind.Slack => (Token("GoodFillColor"), Token("GoodInkColor"), view.SlackSegment, view.SlackSegmentShort, 8d),
            RulerSegmentKind.Drive => (Token("DriveFillColor"), Token("AccentInkColor"), view.DriveSegment, view.DriveSegmentShort, 8d),
            _ => (Token("WalkFillColor"), Token("AmberInkColor"), view.WalkSegment, view.WalkSegment, 5d),
        };

        var room = width - (2 * padding);
        var label = segment.Label switch
        {
            RulerLabelLevel.Full when Fits(full, room) => full,
            RulerLabelLevel.Full or RulerLabelLevel.Short when Fits(shortLabel, room) => shortLabel,
            _ => string.Empty,
        };

        return new Border
        {
            Width = width,
            Height = BlockHeight,
            CornerRadius = new CornerRadius(BlockRadius),
            Background = Brush(fill),
            Padding = new Thickness(padding, 0, padding, 0),
            Child = Text(label, "SegmentLabel", textInk),
        };
    }

    private static FrameworkElement Hatch(double width, Color ground, Color ink, float stroke, float pitch, Color? ring)
    {
        if (!SKCanvasElement.IsSupportedOnCurrentPlatform())
        {
            // No Skia canvas: a flat block in the ground colour still reads as "not your time".
            return new Border { Width = width, Height = BlockHeight, CornerRadius = new CornerRadius(BlockRadius), Background = Brush(ground) };
        }

        return new HatchFill(ToSk(ground), ToSk(ink), stroke, pitch, (float)BlockRadius, ring is { } r ? ToSk(r) : null)
        {
            Width = width,
            Height = BlockHeight,
        };
    }

    private void PlaceKey(string caption, string value, Color valueInk, RulerKeyTime key, double x, double width)
    {
        var stack = new StackPanel { Spacing = 1 };
        var align = key.Align switch
        {
            KeyTimeAlign.Left => HorizontalAlignment.Left,
            KeyTimeAlign.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center,
        };
        var captionText = Text(caption, "TimelineLabel", null);
        var valueText = Text(value, "TimelineTime", valueInk);
        captionText.HorizontalAlignment = align;
        valueText.HorizontalAlignment = align;
        stack.Children.Add(captionText);
        stack.Children.Add(valueText);

        stack.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var w = stack.DesiredSize.Width;

        var left = key.Align switch
        {
            KeyTimeAlign.Left => x + KeyGap,
            KeyTimeAlign.Right => x - w - KeyGap,
            _ => x - (w / 2),
        };

        Place(stack, Math.Clamp(left, 0, Math.Max(0, width - w)), 0);
    }

    private void PlaceCentered(FrameworkElement element, double x, double top, double width)
    {
        element.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var w = element.DesiredSize.Width;
        Place(element, Math.Clamp(x - (w / 2), 0, Math.Max(0, width - w)), top);
    }

    private void Place(FrameworkElement element, double left, double top)
    {
        // The ruler speaks as one image with one name; its pieces are not separate stops.
        AutomationProperties.SetAccessibilityView(element, AccessibilityView.Raw);
        SetLeft(element, left);
        SetTop(element, top);
        Children.Add(element);
    }

    private static Rectangle Line(double width, Color color) => new()
    {
        Width = width,
        Height = LineHeight,
        RadiusX = width / 2,
        RadiusY = width / 2,
        Fill = Brush(color),
    };

    private static TextBlock Text(string text, string styleKey, Color? ink)
    {
        var block = new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.Resources[styleKey],
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };

        if (ink is { } color)
        {
            block.Foreground = Brush(color);
        }

        return block;
    }

    private static bool Fits(string text, double room)
    {
        if (string.IsNullOrEmpty(text) || room <= 0)
        {
            return false;
        }

        var probe = Text(text, "SegmentLabel", null);
        probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        // A pixel of tolerance: the 3-minute walk block fits "Walk" with nothing to spare.
        return probe.DesiredSize.Width <= room + 1;
    }

    private static Color ToneColor(CardTone tone) => tone switch
    {
        CardTone.Late => Token("DangerColor"),
        CardTone.LeaveNow => Token("LeaveNowColor"),
        _ => Token("AccentColor"),
    };

    private static Color ToneInkColor(CardTone tone) => tone switch
    {
        CardTone.Late => Token("DangerInkColor"),
        CardTone.LeaveNow => Token("LeaveNowInkColor"),
        _ => Token("AccentInkColor"),
    };

    private static Color Token(string key) => CardPalette.Lookup(key) is Color color ? color : Colors.Magenta;

    private static Color WithAlpha(Color color, double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), color.R, color.G, color.B);

    private static SolidColorBrush Brush(Color color) => new(color);

    private static SKColor ToSk(Color color) => new(color.R, color.G, color.B, color.A);
}
