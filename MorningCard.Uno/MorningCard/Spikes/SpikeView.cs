#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MorningCard.Controls;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;

namespace MorningCard.Spikes;

/// <summary>
/// v6 step 1 spikes, reached with MC_SPIKE=1. R7: Geist static faces in XAML and Skia text.
/// R8: OSM tiles raw next to tiles muted through <see cref="MutedTileSource"/>. R9: the design's
/// 135° hatch as a repeating LinearGradientBrush next to the same hatch drawn in Skia.
/// Throwaway harness: code-built, DEBUG only, never navigated to.
/// </summary>
internal sealed class SpikeView : ScrollViewer
{
    private const double SchoolLatitude = 45.5250;
    private const double SchoolLongitude = -73.5795;

    private static readonly Color Ink = Color.FromArgb(0xFF, 0x2A, 0x2C, 0x2F);
    private static readonly Color HatchInk = Color.FromArgb(0x1A, 0x2A, 0x2C, 0x2F);
    private static readonly Color HatchGround = Color.FromArgb(0xFF, 0xF1, 0xEF, 0xEA);

    public SpikeView()
    {
        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE9, 0xE6, 0xDF));
        var stack = new StackPanel { Padding = new Thickness(16), Spacing = 10 };
        Content = stack;

        // R7
        stack.Children.Add(Caption("R7 Geist (XAML)"));
        stack.Children.Add(Sample("#Geist", FontWeights.Normal, "Leave by 7:33 · 400 Regular"));
        stack.Children.Add(Sample("#Geist Medium", FontWeights.Medium, "Leave by 7:33 · 500 Medium"));
        stack.Children.Add(Sample("#Geist SemiBold", FontWeights.SemiBold, "Leave by 7:33 · 600 SemiBold"));
        stack.Children.Add(Sample("#Geist Mediumx", FontWeights.Medium, "Leave by 7:33 · wrong fragment"));
        stack.Children.Add(Caption("R7 Geist (Skia, SKTypeface from ms-appx)"));
        stack.Children.Add(new SkiaTextSample { Width = 380, Height = 96, HorizontalAlignment = HorizontalAlignment.Left });

        // R9
        stack.Children.Add(Caption("R9 hatch: gradient Repeat (top) vs Skia lines (bottom), design ink then 100% ink"));
        stack.Children.Add(Row(GradientHatch(HatchInk), GradientHatch(Ink)));
        stack.Children.Add(Row(new SkiaHatch(HatchInk), new SkiaHatch(Ink)));

        // R8
        stack.Children.Add(Caption("R8 OSM raw (top) vs muted toward #EFEEE9 (bottom)"));
        stack.Children.Add(MapSample(muted: false));
        stack.Children.Add(MapSample(muted: true));
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x76, 0x78, 0x7B)),
    };

    private static TextBlock Sample(string fragment, FontWeight weight, string text) => new()
    {
        Text = text,
        FontSize = 24,
        FontWeight = weight,
        FontFamily = new FontFamily($"ms-appx:///Assets/Fonts/Geist_{FileFor(fragment)}.ttf{fragment}"),
        Foreground = new SolidColorBrush(Ink),
    };

    private static string FileFor(string fragment) => fragment switch
    {
        "#Geist" => "Regular",
        "#Geist SemiBold" => "SemiBold",
        _ => "Medium",
    };

    private static StackPanel Row(FrameworkElement a, FrameworkElement b)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(a);
        row.Children.Add(b);
        return row;
    }

    /// <summary>CSS repeating-linear-gradient(135deg, ink 0 1px, transparent 1px 6px).</summary>
    private static Border GradientHatch(Color ink)
    {
        // 135deg runs toward the bottom-right, so one 6 px period along that axis ends at
        // (6/√2, 6/√2). Absolute mapping keeps the pitch in epx regardless of the block's size.
        var d = 6 / Math.Sqrt(2);
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(d, d),
            SpreadMethod = GradientSpreadMethod.Repeat,
        };
        brush.GradientStops.Add(new GradientStop { Color = ink, Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = ink, Offset = 1 / 6.0 });
        brush.GradientStops.Add(new GradientStop { Color = Colors.Transparent, Offset = 1 / 6.0 });
        brush.GradientStops.Add(new GradientStop { Color = Colors.Transparent, Offset = 1 });

        var hatch = new Border { Background = brush };
        return new Border
        {
            Width = 180,
            Height = 30,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(HatchGround),
            Child = hatch,
        };
    }

    private sealed class SkiaHatch : SKCanvasElement
    {
        private readonly SKPaint _ground = new() { Color = ToSk(HatchGround), IsAntialias = true };
        private readonly SKPaint _line;

        public SkiaHatch(Color ink)
        {
            Width = 180;
            Height = 30;
            _line = new SKPaint { Color = ToSk(ink), StrokeWidth = 1, IsAntialias = true, Style = SKPaintStyle.Stroke };
        }

        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            var w = (float)area.Width;
            var h = (float)area.Height;
            var clip = new SKRoundRect(new SKRect(0, 0, w, h), 6);

            canvas.Save();
            canvas.ClipRoundRect(clip, antialias: true);
            canvas.DrawRoundRect(clip, _ground);

            // Lines x + y = c, 6 px apart measured perpendicular, so 6√2 apart along x.
            var step = 6f * MathF.Sqrt(2);
            for (var c = 0.5f; c < w + h; c += step)
            {
                canvas.DrawLine(c, 0, c - h, h, _line);
            }

            canvas.Restore();
        }
    }

    private sealed class SkiaTextSample : SKCanvasElement
    {
        private static readonly string[] Faces = ["Regular", "Medium", "SemiBold"];
        private readonly SKTypeface?[] _faces = new SKTypeface?[3];
        private readonly SKPaint _ink = new() { Color = ToSk(Ink), IsAntialias = true };

        public SkiaTextSample()
        {
            Loaded += async (_, _) =>
            {
                for (var i = 0; i < Faces.Length; i++)
                {
                    try
                    {
                        var file = await StorageFile.GetFileFromApplicationUriAsync(
                            new Uri($"ms-appx:///Assets/Fonts/Geist_{Faces[i]}.ttf"));
                        using var stream = await file.OpenStreamForReadAsync();
                        using var memory = new MemoryStream();
                        await stream.CopyToAsync(memory);
                        memory.Position = 0;
                        _faces[i] = SKTypeface.FromStream(memory);
                    }
                    catch (Exception error)
                    {
                        System.Diagnostics.Debug.WriteLine($"Spike: Geist_{Faces[i]} failed: {error.Message}");
                    }
                }

                Invalidate();
            };
        }

        protected override void RenderOverride(SKCanvas canvas, Size area)
        {
            for (var i = 0; i < Faces.Length; i++)
            {
                using var font = new SKFont(_faces[i] ?? SKTypeface.Default, 19);
                var label = $"7:33  7:50  {Faces[i]} · {_faces[i]?.FamilyName ?? "FALLBACK"}";
                canvas.DrawText(label, 0, 26 + (i * 30), font, _ink);
            }
        }
    }

    private static FrameworkElement MapSample(bool muted)
    {
        const string agent = "MorningCard/1.0 (Uno Platform sample; https://github.com/mtmattei/SampleBuilds)";

        var map = new Mapsui.Map();
        var osm = Mapsui.Tiling.OpenStreetMap.CreateTileLayer(agent);
        map.Layers.Add(muted
            ? new Mapsui.Tiling.Layers.TileLayer(new MutedTileSource(
                (BruTile.IHttpTileSource)osm.TileSource,
                MutedTileSource.PaperMatrix(new SKColor(0xEF, 0xEE, 0xE9))))
            : osm);

        var control = new Mapsui.UI.WinUI.MapControl { Height = 190 };
        control.Map = map;

        var school = Mapsui.Projections.SphericalMercator.FromLonLat(SchoolLongitude, SchoolLatitude);
        control.SizeChanged += (_, e) =>
        {
            if (e.NewSize.Width > 0)
            {
                map.Navigator.CenterOnAndZoomTo(new Mapsui.MPoint(school.x, school.y), 4.8);
            }
        };

        return new Border { CornerRadius = new CornerRadius(16), Child = control };
    }

    private static SKColor ToSk(Color c) => new(c.R, c.G, c.B, c.A);
}
#endif
