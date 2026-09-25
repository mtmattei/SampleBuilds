using Microsoft.UI;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Shapes;
using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Presentation;
using MorningCard.Themes;
using Windows.Foundation;
using Windows.UI;

namespace MorningCard.Controls;

/// <summary>
/// The watch's slack ruler, bent around the bezel: the same 36-minute window and blocks as
/// <see cref="SlackRuler"/> (the plan is <see cref="SlackRulerLayout"/>'s), laid on a 270° ring
/// that opens at the bottom for Start drive. Minute 0 sits at 7:30 on the dial and the window runs
/// clockwise over the top, so the day still reads left to right.
///
/// Laid out in the watch page's 454-unit face; the host's Viewbox scales it. Arcs are XAML paths
/// (circular arcs only, which Uno's path parser accepts); the past and after blocks are hatched
/// with a repeating gradient brush.
/// </summary>
public sealed partial class RadialSlackRuler : Canvas
{
    public static readonly DependencyProperty ViewProperty = DependencyProperty.Register(
        nameof(View), typeof(EtaView), typeof(RadialSlackRuler), new PropertyMetadata(null, (d, _) => ((RadialSlackRuler)d).Rebuild()));

    private const double Face = 454;
    private const double Center = Face / 2;

    // The ring: 270° from 7:30 on the dial, clockwise. Band centred at 206, 14 thick.
    private const double StartAngle = 225;
    private const double Sweep = 270;
    private const double BandRadius = 206;
    private const double BandThickness = 14;
    private const double GapDegrees = 0.7;

    // Key-time captions sit this far in from the centre, inside the band.
    private const double LabelRadius = 174;

    // Two stacked captions (11 + 17) need about this much arc at LabelRadius to clear each other.
    private const double MinLabelDegrees = 18;

    // 4 o'clock (120°, unwrapped past the ring's 225° start): the lowest a caption can sit before
    // it meets the Start drive button across the bottom of the face.
    private const double MaxLabelAngle = 360 + 120;

    public RadialSlackRuler()
    {
        Width = Face;
        Height = Face;
        IsHitTestVisible = false;
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

        if (View is not { Slack: { } plan } view)
        {
            AutomationProperties.SetName(this, string.Empty);
            return;
        }

        AutomationProperties.SetName(this, view.RulerDescription);

        double A(int minute) => StartAngle + (minute / (double)plan.WindowMinutes * Sweep);

        var ink = Token("InkColor");
        var tone = ToneColor(view.Tone);
        var toneInk = ToneInkColor(view.Tone);

        // Minute ticks just inside the band, heavier every five.
        for (var m = 0; m <= plan.WindowMinutes; m++)
        {
            var major = m % 5 == 0;
            AddPart(Radial(A(m), major ? 191 : 194, 197, 1, WithAlpha(ink, major ? 0.28 : 0.13)));
        }

        foreach (var segment in plan.Segments.Where(s => s.Kind != RulerSegmentKind.Overrun))
        {
            var from = A(segment.StartMin) + GapDegrees;
            var to = A(segment.EndMin) - GapDegrees;
            if (to <= from)
            {
                continue;
            }

            AddPart(segment.Kind switch
            {
                RulerSegmentKind.Past or RulerSegmentKind.After => Arc(from, to, BandRadius, BandThickness, Hatch(Token("HatchGroundColor"), Token("HatchColor"))),
                RulerSegmentKind.Slack => Arc(from, to, BandRadius, BandThickness, Brush(Token("GoodFillColor"))),
                RulerSegmentKind.Drive => Arc(from, to, BandRadius, BandThickness, Brush(Token("DriveFillColor"))),
                _ => Arc(from, to, BandRadius, BandThickness, Brush(Token("WalkFillColor"))),
            });
        }

        if (plan.Find(RulerSegmentKind.Overrun) is { } overrun)
        {
            AddPart(Arc(A(overrun.StartMin) + GapDegrees, A(overrun.EndMin) - GapDegrees, BandRadius, BandThickness,
                Hatch(WithAlpha(Token("OverrunColor"), 0.35), Token("OverrunRingColor"))));
        }

        // Key-time lines across the band.
        if (plan.Leave.ShowLine)
        {
            AddPart(Radial(A(plan.Leave.AtMin), 196, 216, 1.5, WithAlpha(Token("AccentColor"), 0.55)));
        }

        AddPart(Radial(A(plan.Arrive.AtMin), 196, 216, 1.5, WithAlpha(ink, 0.35)));
        AddPart(Radial(A(plan.Bell.AtMin), 196, 216, 2, ink));

        // Now: a line in from the band and a dot on a face-coloured ring.
        var now = A(plan.NowMin);
        AddPart(Radial(now, 192, BandRadius, 1.5, tone));
        if (!plan.LeaveIsNow)
        {
            AddPart(Dot(now, BandRadius, 13, Token("WatchFaceColor")));
            AddPart(Dot(now, BandRadius, 9, tone));
        }

        // Leaving now: LEAVE already sits at now, so NOW would print on top of it.
        if (!plan.LeaveIsNow)
        {
            PlaceLabel(Caption(view.NowShortCaption, toneInk), now, 170);
        }

        // LEAVE, ARRIVE and BELL can share a minute (leaving now at the bell, late) and are one
        // minute (7.5°) apart when late: walk them clockwise and keep each MinLabelDegrees past the
        // one before.
        var keys = new[]
        {
            (Caption: view.LeaveCaption, Value: view.LeaveValue, Ink: toneInk, Angle: A(plan.Leave.AtMin)),
            (Caption: view.ArriveCaption, Value: view.ArriveTime, Ink: view.IsLate ? Token("DangerInkColor") : ink, Angle: A(plan.Arrive.AtMin)),
            (Caption: view.BellCaption, Value: view.BellTime, Ink: ink, Angle: A(plan.Bell.AtMin)),
        }.OrderBy(k => k.Angle).ToArray();

        for (var i = 1; i < keys.Length; i++)
        {
            keys[i].Angle = Math.Max(keys[i].Angle, keys[i - 1].Angle + MinLabelDegrees);
        }

        // Past 4 o'clock a caption runs into Start drive: pull the last one back and the rest with it.
        if (keys[^1].Angle > MaxLabelAngle)
        {
            keys[^1].Angle = MaxLabelAngle;
            for (var i = keys.Length - 2; i >= 0; i--)
            {
                keys[i].Angle = Math.Min(keys[i].Angle, keys[i + 1].Angle - MinLabelDegrees);
            }
        }

        foreach (var key in keys)
        {
            PlaceKey(key.Caption, key.Value, key.Ink, key.Angle);
        }
    }

    private void PlaceKey(string caption, string value, Color valueInk, double angle)
    {
        var stack = new StackPanel { Spacing = 0 };
        var side = Side(angle);
        var captionText = Caption(caption, null);
        var valueText = Text(value, 17, valueInk);
        captionText.HorizontalAlignment = side;
        valueText.HorizontalAlignment = side;
        stack.Children.Add(captionText);
        stack.Children.Add(valueText);
        PlaceLabel(stack, angle, LabelRadius);
    }

    /// <summary>
    /// Anchors a label at a point inside the ring: left half starts there, right half ends there,
    /// the top and bottom centre on it; always vertically centred.
    /// </summary>
    private void PlaceLabel(FrameworkElement element, double angle, double radius)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (w, h) = (element.DesiredSize.Width, element.DesiredSize.Height);
        var p = Polar(angle, radius);

        var left = Side(angle) switch
        {
            HorizontalAlignment.Left => p.X,
            HorizontalAlignment.Right => p.X - w,
            _ => p.X - (w / 2),
        };

        AutomationProperties.SetAccessibilityView(element, AccessibilityView.Raw);
        SetLeft(element, left);
        SetTop(element, p.Y - (h / 2));
        Children.Add(element);
    }

    private static HorizontalAlignment Side(double angle)
    {
        var s = Math.Sin(angle * Math.PI / 180);
        return s < -0.35 ? HorizontalAlignment.Left : s > 0.35 ? HorizontalAlignment.Right : HorizontalAlignment.Center;
    }

    private void AddPart(UIElement element)
    {
        AutomationProperties.SetAccessibilityView(element, AccessibilityView.Raw);
        Children.Add(element);
    }

    private static Point Polar(double angle, double radius)
    {
        var r = angle * Math.PI / 180;
        return new Point(Center + (radius * Math.Sin(r)), Center - (radius * Math.Cos(r)));
    }

    private static Microsoft.UI.Xaml.Shapes.Path Arc(double from, double to, double radius, double thickness, Brush stroke)
    {
        var figure = new PathFigure { StartPoint = Polar(from, radius), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = Polar(to, radius),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = to - from > 180,
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = thickness,
        };
    }

    private static Line Radial(double angle, double inner, double outer, double thickness, Color color)
    {
        var (a, b) = (Polar(angle, inner), Polar(angle, outer));
        return new Line
        {
            X1 = a.X,
            Y1 = a.Y,
            X2 = b.X,
            Y2 = b.Y,
            Stroke = Brush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
    }

    private static Ellipse Dot(double angle, double radius, double size, Color color)
    {
        var p = Polar(angle, radius);
        var dot = new Ellipse { Width = size, Height = size, Fill = Brush(color) };
        SetLeft(dot, p.X - (size / 2));
        SetTop(dot, p.Y - (size / 2));
        return dot;
    }

    /// <summary>1 px diagonal lines every 6 px over the ground, the straight ruler's hatch.</summary>
    private static LinearGradientBrush Hatch(Color ground, Color line)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            SpreadMethod = GradientSpreadMethod.Repeat,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(4.24, 4.24),
        };
        brush.GradientStops.Add(new GradientStop { Color = line, Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = line, Offset = 0.2 });
        brush.GradientStops.Add(new GradientStop { Color = ground, Offset = 0.2 });
        brush.GradientStops.Add(new GradientStop { Color = ground, Offset = 1 });
        return brush;
    }

    // Caps without the design's 0.08em tracking: CharacterSpacing is a no-op on the Skia text stack.
    private static TextBlock Caption(string text, Color? ink) => Text(text, 11, ink ?? Token("CapsColor"));

    private static TextBlock Text(string text, double size, Color ink) => new()
    {
        Text = text,
        FontSize = size,
        FontFamily = (FontFamily)Application.Current.Resources["CardFontSemiBold"],
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = Brush(ink),
        TextWrapping = TextWrapping.NoWrap,
    };

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
}
