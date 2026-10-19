#if DEBUG
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using MorningCard.Core;
using MorningCard.Core.Services;

namespace MorningCard;

/// <summary>
/// A read-only JSON picture of the running app, so a verification run can assert on what the card
/// is saying instead of guessing from a screenshot. DEBUG only, and inert unless MC_SNAPSHOT is set.
///
/// <list type="bullet">
/// <item><c>MC_SNAPSHOT</c>: file to write. Rewritten once a second, atomically, so a reader never
/// sees half a file.</item>
/// <item><c>MC_EXIT_AFTER</c>: seconds. Writes a final snapshot and closes the app, so one check
/// costs exactly one launch and no window is left behind.</item>
/// </list>
///
/// Three sections, from most to least trusted:
/// <c>hub</c> is the simulator's state, <c>derived</c> is <see cref="TripMath.Derive"/> on it (what
/// the card should say), and <c>ui</c> is what the visual tree actually shows. A disagreement
/// between <c>derived</c> and <c>ui</c> is a binding or template bug.
/// </summary>
internal static class DebugSnapshot
{
    private static long _sequence;

    public static void Start(UIElement root, SimulatedDriveHub hub)
    {
        if (Read("MC_SNAPSHOT") is not { } path)
        {
            return;
        }

        var exitAfter = int.TryParse(Read("MC_EXIT_AFTER"), out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : (TimeSpan?)null;
        var started = DateTimeOffset.Now;

        var timer = root.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) =>
        {
            Write(path, root, hub);

            if (exitAfter is { } limit && DateTimeOffset.Now - started >= limit)
            {
                timer.Stop();
                DebugHooks.Trace($"SNAPSHOT: exiting after {limit.TotalSeconds:0}s, last write #{_sequence}");
                Application.Current.Exit();
            }
        };
        timer.Start();
    }

    private static void Write(string path, UIElement root, SimulatedDriveHub hub)
    {
        try
        {
            var buffer = new MemoryStream();

            using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();
                json.WriteNumber("seq", ++_sequence);
                json.WriteString("writtenAt", DateTimeOffset.Now);

                WriteHub(json, hub.State);
                WriteDerived(json, hub.State);
                WriteUi(json, root);

                json.WriteEndObject();
            }

            // Write beside the target and move over it, so a poller never reads a torn file.
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, buffer.ToArray());
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // A snapshot that cannot be written must not take the app with it.
            DebugHooks.Trace($"SNAPSHOT: write failed: {ex.Message}");
        }
    }

    private static void WriteHub(Utf8JsonWriter json, DriveHubState state)
    {
        json.WriteStartObject("hub");
        json.WriteString("mode", state.Mode.ToString());
        json.WriteBoolean("faulted", state.Faulted);
        json.WriteString("clock", TripMath.FromMinutes(state.ClockMinutes).ToString("HH:mm"));
        json.WriteNumber("delayMin", state.Trip.DelayMin);
        json.WriteNumber("rangeKm", state.Vehicle.Range);
        json.WriteBoolean("vehicleStale", state.Vehicle.IsStale);
        json.WriteNumber("revision", state.Revision);
        json.WriteEndObject();
    }

    private static void WriteDerived(Utf8JsonWriter json, DriveHubState state)
    {
        var t = TripMath.Derive(state.Trip, TripMath.FromMinutes(state.ClockMinutes));

        json.WriteStartObject("derived");
        json.WriteString("tone", t.Tone.ToString());
        json.WriteString("leaveBy", t.LeaveBy.ToString("HH:mm"));
        json.WriteNumber("slackMin", t.SlackMin);
        json.WriteBoolean("isLate", t.IsLate);
        json.WriteString("arrive", t.Arrive.ToString("HH:mm"));
        json.WriteNumber("totalMin", t.TotalMin);
        json.WriteString("traffic", t.Traffic.ToString());
        json.WriteString("rangeLevel", TripMath.RangeLevel(state.Vehicle.Range).ToString());
        json.WriteEndObject();
    }

    private static void WriteUi(Utf8JsonWriter json, UIElement root)
    {
        var named = new List<(string Name, string Type, bool Visible, string? Text)>();
        var text = new List<string>();
        Walk(root, parentVisible: true, inTemplate: false, named, text);

        json.WriteStartObject("ui");
        json.WriteString("theme", (root as FrameworkElement)?.ActualTheme.ToString());

        // Every x:Name the app declares, with whether a person could actually see it. Names inside
        // stock control templates (PART_Border, SliderContainer...) are left out.
        json.WriteStartArray("named");
        foreach (var (name, type, visible, content) in named)
        {
            json.WriteStartObject();
            json.WriteString("name", name);
            json.WriteString("type", type);
            json.WriteBoolean("visible", visible);
            if (content is not null)
            {
                json.WriteString("text", content);
            }

            json.WriteEndObject();
        }

        json.WriteEndArray();

        // The words on screen, in tree order. This is what assertions usually want:
        // "does the card say 'Running 6 min late' right now".
        json.WriteStartArray("visibleText");
        foreach (var line in text.Distinct())
        {
            json.WriteStringValue(line);
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    /// <param name="inTemplate">
    /// True while walking the insides of a stock control's template. A presenter hands back to the
    /// app's own content, so names under a FeedView's or ScrollViewer's content still count.
    /// </param>
    private static void Walk(DependencyObject element, bool parentVisible, bool inTemplate, List<(string, string, bool, string?)> named, List<string> text)
    {
        var visible = parentVisible;

        if (element is UIElement ui)
        {
            visible &= ui.Visibility == Visibility.Visible && ui.Opacity > 0.01;
        }

        if (element is FrameworkElement fe)
        {
            visible &= fe.ActualWidth > 0 && fe.ActualHeight > 0;

            var content = TextOf(fe);

            if (!inTemplate && !string.IsNullOrEmpty(fe.Name) && !IsTemplatePartName(fe.Name))
            {
                named.Add((fe.Name, fe.GetType().Name, visible, content));
            }

            if (visible && fe is TextBlock && !string.IsNullOrWhiteSpace(content) && !IsIconGlyph(content))
            {
                text.Add(content);
            }
        }

        var childInTemplate = element switch
        {
            ContentPresenter or ItemsPresenter => false,
            Control control when IsStock(control) => true,
            _ => inTemplate,
        };

        var count = VisualTreeHelper.GetChildrenCount(element);

        for (var i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(element, i), visible, childInTemplate, named, text);
        }
    }

    /// <summary>A control whose template the app did not write: WinUI, Uno, Toolkit, Mapsui.</summary>
    private static bool IsStock(Control control) =>
        control.GetType().Namespace is { } ns && !ns.StartsWith(nameof(MorningCard), StringComparison.Ordinal);

    /// <summary>
    /// Stock templates nest presenters inside presenters (Toolkit's Chip hands back to its own parts
    /// before the app's content), so the structural rule above lets a few through. They follow two
    /// naming conventions the app's own names do not.
    /// </summary>
    private static bool IsTemplatePartName(string name) =>
        name.StartsWith("PART_", StringComparison.Ordinal) || name.EndsWith("Presenter", StringComparison.Ordinal);

    /// <summary>Phosphor glyphs live in the Private Use Area; they are icons, not words.</summary>
    private static bool IsIconGlyph(string value) => value.All(c => c is >= '' and <= '' || char.IsWhiteSpace(c));

    private static string? TextOf(FrameworkElement element) => element switch
    {
        TextBlock { Text.Length: > 0 } block => block.Text,

        // Headlines built from Runs leave Text empty; the words live in the inlines.
        TextBlock block when block.Inlines.Count > 0 => Flatten(block.Inlines),
        ContentControl { Content: string value } => value,
        _ => null,
    };

    private static string Flatten(InlineCollection inlines)
    {
        var sb = new StringBuilder();

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    sb.Append(run.Text);
                    break;
                case Span span:
                    sb.Append(Flatten(span.Inlines));
                    break;
                case LineBreak:
                    sb.Append(' ');
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
#endif
