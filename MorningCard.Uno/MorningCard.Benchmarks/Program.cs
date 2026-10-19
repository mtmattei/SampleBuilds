using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using MorningCard.Core;
using MorningCard.Core.Models;
using MorningCard.Core.Route;
using MorningCard.Core.Services;

// CPU benchmarks for the Core paths the card runs on every clock tick.
//
//   Measure:  dotnet run -c Release --project MorningCard.Benchmarks -- [--route <route.json>] [--out <file.json>] [--label <text>]
//   Compare:  dotnet run -c Release --project MorningCard.Benchmarks -- --compare <baseline.json> <current.json> [--summary <file.md>] [--fail-over <percent>]
//
// Numbers are medians of timed batches on one machine. They compare two commits measured in the
// same job; they are not frame rates and say nothing about the Skia renderer or the UI thread.

if (args.Length > 0 && args[0] == "--compare")
{
    return Compare.Run(args);
}

var routePath = Arg(args, "--route") ?? Path.Combine("MorningCard", "Assets", "route.json");
var outPath = Arg(args, "--out");
var label = Arg(args, "--label") ?? "local";

if (!File.Exists(routePath))
{
    Console.Error.WriteLine($"Route asset not found at {routePath}. Run from MorningCard.Uno or pass --route.");
    return 2;
}

var json = File.ReadAllText(routePath);
var geometry = RouteAsset.TryParse(json)
    ?? throw new InvalidOperationException($"{routePath} parsed to nothing.");

var plan = DriveHubState.Default.Trip;
var morning = new TimeOnly(7, 24);
var timeline = TripMath.Derive(plan, morning);

// The interval the card would choose at a typical fitted resolution; fixed so both commits run the
// same layout work.
var interval = TimeRulerLayout.ChooseInterval(geometry, metersPerPixel: 2d);

var results = new List<BenchResult>
{
    Bench.Measure("TripMath.Derive", () => TripMath.Derive(plan, morning)),
    Bench.Measure("RouteAsset.TryParse", () => RouteAsset.TryParse(json)!),
    Bench.Measure("TimeRulerLayout.ChooseInterval", () => TimeRulerLayout.ChooseInterval(geometry, 2d)),
    Bench.Measure("TimeRulerLayout.Compute (7:24)", () =>
        TimeRulerLayout.Compute(geometry, timeline, plan.WalkMin, plan.Bell, morning, interval)),

    // What the card actually does as the clock runs: one layout per minute across the morning,
    // through on time, leave now and late.
    Bench.Measure("Clock sweep 7:00-8:10 (71 layouts)", () =>
    {
        RulerLayout? last = null;

        for (var minute = 7 * 60; minute <= (8 * 60) + 10; minute++)
        {
            var now = TripMath.FromMinutes(minute);
            last = TimeRulerLayout.Compute(geometry, TripMath.Derive(plan, now), plan.WalkMin, plan.Bell, now, interval);
        }

        return last!;
    }),
};

var report = new BenchReport(
    Label: label,
    Runtime: RuntimeInformation.FrameworkDescription,
    Os: RuntimeInformation.OSDescription,
    Processors: Environment.ProcessorCount,
    Results: results);

foreach (var r in results)
{
    Console.WriteLine($"{r.Name,-40} {Format.Time(r.MedianNsPerOp),12}/op  {Format.Bytes(r.AllocatedBytesPerOp),10}/op");
}

if (outPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllText(outPath, JsonSerializer.Serialize(report, Json.Options));
    Console.WriteLine($"Wrote {outPath}");
}

return 0;

static string? Arg(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

internal sealed record BenchResult(string Name, double MedianNsPerOp, double AllocatedBytesPerOp, long OpsPerSample, int Samples);

internal sealed record BenchReport(string Label, string Runtime, string Os, int Processors, IReadOnlyList<BenchResult> Results);

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
}

internal static class Bench
{
    private const int Samples = 15;
    private static readonly TimeSpan SampleTarget = TimeSpan.FromMilliseconds(20);

    // Results go here so the JIT cannot prove an operation unused and delete it.
    private static object? _sink;

    public static BenchResult Measure(string name, Func<object> op)
    {
        // Warm up past tiering, then size a batch so one sample takes about SampleTarget.
        var warmUntil = Stopwatch.StartNew();
        while (warmUntil.ElapsedMilliseconds < 250)
        {
            _sink = op();
        }

        long ops = 1;
        while (true)
        {
            var sw = Stopwatch.StartNew();
            for (long i = 0; i < ops; i++)
            {
                _sink = op();
            }

            if (sw.Elapsed >= SampleTarget || ops >= 1L << 30)
            {
                break;
            }

            ops *= 2;
        }

        var perOpNs = new double[Samples];
        long allocated = 0;

        for (var s = 0; s < Samples; s++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            for (long i = 0; i < ops; i++)
            {
                _sink = op();
            }

            sw.Stop();
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            perOpNs[s] = sw.Elapsed.TotalNanoseconds / ops;
        }

        Array.Sort(perOpNs);
        GC.KeepAlive(_sink);

        return new BenchResult(name, perOpNs[Samples / 2], (double)allocated / (ops * Samples), ops, Samples);
    }
}

internal static class Compare
{
    // Unchanged code measured ±10% run to run on a 2-core runner; below this a change is noise.
    private const double NoiseBandPercent = 20d;

    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: --compare <baseline.json> <current.json> [--summary <file.md>] [--fail-over <percent>]");
            return 2;
        }

        var baseline = Load(args[1]);
        var current = Load(args[2]);
        var summaryPath = Index(args, "--summary") is { } si ? args[si + 1] : null;
        var failOver = Index(args, "--fail-over") is { } fi ? double.Parse(args[fi + 1], System.Globalization.CultureInfo.InvariantCulture) : (double?)null;

        var md = new StringBuilder();
        md.AppendLine($"### MorningCard.Core benchmarks: {current.Label} vs {baseline.Label}");
        md.AppendLine();
        md.AppendLine($"{current.Runtime} · {current.Os} · {current.Processors} cores. Median of timed batches; changes within ±{NoiseBandPercent:0}% are treated as noise.");
        md.AppendLine();
        md.AppendLine("| Benchmark | Baseline | Current | Change | Alloc baseline → current |");
        md.AppendLine("| --- | ---: | ---: | ---: | ---: |");

        var worst = 0d;

        foreach (var now in current.Results)
        {
            var then = baseline.Results.FirstOrDefault(r => r.Name == now.Name);

            if (then is null)
            {
                md.AppendLine($"| {now.Name} | new | {Format.Time(now.MedianNsPerOp)} | | {Format.Bytes(now.AllocatedBytesPerOp)} |");
                continue;
            }

            var change = ((now.MedianNsPerOp / then.MedianNsPerOp) - 1d) * 100d;
            worst = Math.Max(worst, change);

            var verdict = change > NoiseBandPercent ? "slower" : change < -NoiseBandPercent ? "faster" : "same";
            md.AppendLine($"| {now.Name} | {Format.Time(then.MedianNsPerOp)} | {Format.Time(now.MedianNsPerOp)} | {change:+0.0;-0.0}% {verdict} | {Format.Bytes(then.AllocatedBytesPerOp)} → {Format.Bytes(now.AllocatedBytesPerOp)} |");
        }

        foreach (var gone in baseline.Results.Where(r => current.Results.All(c => c.Name != r.Name)))
        {
            md.AppendLine($"| {gone.Name} | {Format.Time(gone.MedianNsPerOp)} | removed | | |");
        }

        Console.Write(md);

        if (summaryPath is not null)
        {
            File.AppendAllText(summaryPath, md.ToString());
        }

        if (failOver is { } limit && worst > limit)
        {
            Console.Error.WriteLine($"Slowest regression is {worst:0.0}%, over the {limit:0}% limit.");
            return 1;
        }

        return 0;
    }

    private static int? Index(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? i : null;
    }

    private static BenchReport Load(string path) =>
        JsonSerializer.Deserialize<BenchReport>(File.ReadAllText(path), Json.Options)
        ?? throw new InvalidDataException($"{path} is not a benchmark report.");
}

internal static class Format
{
    public static string Time(double ns) => ns switch
    {
        < 1_000 => $"{ns:0.0} ns",
        < 1_000_000 => $"{ns / 1_000:0.00} µs",
        _ => $"{ns / 1_000_000:0.00} ms",
    };

    public static string Bytes(double bytes) => bytes switch
    {
        < 1 => "0 B",
        < 1024 => $"{bytes:0} B",
        _ => $"{bytes / 1024:0.0} KB",
    };
}
