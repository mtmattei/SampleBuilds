using System.Xml.Linq;
using MorningCard.Core.Models;
using Xunit;

namespace MorningCard.Tests;

/// <summary>
/// Strings the app reads by a key it builds at runtime, which no text search finds by name. A
/// dead-code pass removed the Traffic_ strings once and the card showed "Traffic_Light".
/// </summary>
public class ResourceKeyTests
{
    private static readonly HashSet<string> Keys = LoadKeys();

    [Fact]
    public void Every_traffic_level_has_its_string() =>
        Assert.All(Enum.GetValues<TrafficLevel>(), level => Assert.Contains($"Traffic_{level}", Keys));

    private static HashSet<string> LoadKeys()
    {
        // The test runs from bin/..., so walk up to the solution folder for the app's .resw.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MorningCard", "Strings", "en", "Resources.resw")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var resw = XDocument.Load(Path.Combine(dir.FullName, "MorningCard", "Strings", "en", "Resources.resw"));
        return resw.Root!.Elements("data").Select(e => (string)e.Attribute("name")!).ToHashSet();
    }
}
