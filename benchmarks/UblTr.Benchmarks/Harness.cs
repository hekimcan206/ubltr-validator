using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using UblTr.SampleGen;
using UblTr.Validator;

namespace UblTr.Benchmarks;

/// <summary>
/// Throughput and memory harness. Every number is the median of several timed rounds, each round running for at
/// least a minimum wall time, after a warm-up. Results go to stdout (Markdown) and benchmark-results/*.json.
/// </summary>
internal static class Harness
{
    // UBLTR_BENCH_LINES=1000,10000 restricts the sizes for a quick before/after comparison.
    private static readonly int[] LineCounts = System.Environment.GetEnvironmentVariable("UBLTR_BENCH_LINES") is { Length: > 0 } only
        ? [.. only.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture))]
        : [1, 10, 100, 1_000, 10_000, 50_000];
    private const int Rounds = 7;
    private static readonly TimeSpan MinRoundTime = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan SliceTime = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// The host is a shared, loaded laptop whose speed changes from minute to minute, so measuring one configuration
    /// for a few seconds and moving on would let load spikes land on whole rows. Instead every (size, mode) pair is
    /// measured in short slices, round-robin in a shuffled order, for several rounds spread over the whole run; the
    /// table reports the median slice and the slowest/fastest slice so the noise is visible.
    /// </summary>
    public static int Throughput(string outDir)
    {
        var validator = new InvoiceValidator(GibAssets.LoadDefault());
        if (validator.Assets.Schemas is null)
        {
            Console.Error.WriteLine("GIB schemas not found next to the benchmark binary; build first.");
            return 2;
        }

        Console.WriteLine($"Schema set compile time: {validator.Assets.SchemaCompileTime.TotalMilliseconds:F0} ms");
        var combos = new List<Combo>();
        foreach (var lines in LineCounts)
        {
            var bytes = Encoding.UTF8.GetBytes(InvoiceWriter.ToXmlString(SyntheticData.RandomInvoice(seed: lines, lineCount: lines) with { LineTaxTotals = true }));
            var check = validator.Validate(new MemoryStream(bytes));
            if (!check.IsValid || check.Findings.Count > 0)
            {
                throw new InvalidOperationException($"benchmark invoice with {lines} lines is not clean");
            }

            combos.AddRange(Modes(validator, bytes).Select(m => new Combo(lines, bytes.Length, m.Mode, m.Action)));
        }

        foreach (var combo in combos)
        {
            combo.Action();
            combo.Action();
        }

        var random = new Random(42);
        for (var round = 0; round < Rounds; round++)
        {
            foreach (var combo in combos.OrderBy(_ => random.Next()))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var ops = 0;
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                do
                {
                    combo.Action();
                    ops++;
                }
                while (sw.Elapsed < SliceTime);

                sw.Stop();
                combo.Rates.Add(ops / sw.Elapsed.TotalSeconds);
                combo.Allocations.Add((GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / (double)ops);
            }

            Console.Error.WriteLine($"round {round + 1}/{Rounds} done");
        }

        var rows = combos.Select(c =>
        {
            var median = Median(c.Rates);
            return new ThroughputRow(c.Lines, c.Bytes, c.Mode, median, median * c.Bytes / (1024d * 1024d), 1000d / median,
                (long)Median(c.Allocations), c.Rates.Min(), c.Rates.Max());
        }).ToList();
        Console.WriteLine("| lines | KB | mode | docs/s (median) | slowest-fastest slice | MB/s | ms/doc | allocated KB/doc |");
        foreach (var r in rows)
        {
            Console.WriteLine($"| {r.Lines} | {r.Bytes / 1024d:F1} | {r.Mode} | {r.DocsPerSecond:F1} | {r.SlowestSlice:F1}-{r.FastestSlice:F1} | {r.MegabytesPerSecond:F2} | {r.MillisecondsPerDoc:F2} | {r.AllocatedBytesPerDoc / 1024d:F0} |");
        }

        Write(outDir, "throughput.json", new { environment = Environment(), rounds = Rounds, sliceMs = SliceTime.TotalMilliseconds, rows });
        return 0;
    }

    private sealed record Combo(int Lines, int Bytes, string Mode, Action Action)
    {
        public List<double> Rates { get; } = [];

        public List<double> Allocations { get; } = [];
    }

    /// <summary>Docs/s with N threads validating 100-line invoices in parallel (the CLI's Parallel.For path).</summary>
    public static int Scaling(string outDir)
    {
        var validator = new InvoiceValidator(GibAssets.LoadDefault());
        var docs = Enumerable.Range(0, 64)
            .Select(i => Encoding.UTF8.GetBytes(InvoiceWriter.ToXmlString(SyntheticData.RandomInvoice(1000 + i, 100)))).ToArray();
        var threadCounts = new[] { 1, 2, 4, 8 }.Where(t => t <= System.Environment.ProcessorCount).ToArray();
        var samples = threadCounts.ToDictionary(t => t, _ => new List<double>());
        var random = new Random(7);

        // Thread counts are interleaved in shuffled order every round, so host load changes hit all of them alike.
        for (var round = 0; round < Rounds + 1; round++)
        {
            foreach (var threads in threadCounts.OrderBy(_ => random.Next()))
            {
                var count = 0L;
                var sw = Stopwatch.StartNew();
                Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
                {
                    var i = t;
                    while (sw.Elapsed < MinRoundTime)
                    {
                        validator.Validate(new MemoryStream(docs[i++ % docs.Length]));
                        Interlocked.Increment(ref count);
                    }
                });
                if (round > 0)
                {
                    samples[threads].Add(count / sw.Elapsed.TotalSeconds);
                }
            }
        }

        var single = Median(samples[1]);
        var rows = threadCounts.Select(t => new { threads = t, docsPerSecond = Median(samples[t]), speedup = Median(samples[t]) / single, samples = samples[t] }).ToList();
        foreach (var row in rows)
        {
            Console.WriteLine($"| {row.threads} | {row.docsPerSecond:F0} | {row.speedup:F2}x |");
        }

        Write(outDir, "scaling.json", new { environment = Environment(), lines = 100, rows });
        return 0;
    }

    /// <summary>Peak working set of this process after validating one file once (run in a fresh process per file).</summary>
    public static int Peak(string file)
    {
        var validator = new InvoiceValidator(GibAssets.LoadDefault());
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var baseline = process.PeakWorkingSet64;
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var sw = Stopwatch.StartNew();
        var result = validator.Validate(file);
        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        process.Refresh();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            file = Path.GetFileName(file),
            bytes = new FileInfo(file).Length,
            valid = result.IsValid,
            lines = result.Document?.LineCount,
            elapsedMs = sw.Elapsed.TotalMilliseconds,
            allocatedBytes = allocated,
            peakWorkingSetBeforeBytes = baseline,
            peakWorkingSetBytes = process.PeakWorkingSet64,
        }));
        return 0;
    }

    private static IEnumerable<(string Mode, Action Action)> Modes(InvoiceValidator validator, byte[] bytes)
    {
        var parseSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
        yield return ("parse-only", () =>
        {
            using var reader = XmlReader.Create(new MemoryStream(bytes), parseSettings);
            XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        );
        yield return ("xsd-only", () => validator.Validate(new MemoryStream(bytes), new ValidationOptions { SkipRules = true }));
        yield return ("rules-only", () => validator.Validate(new MemoryStream(bytes), new ValidationOptions { SkipSchema = true }));
        yield return ("full", () => validator.Validate(new MemoryStream(bytes)));
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }

    private static object Environment() => new
    {
        os = RuntimeInformation.OSDescription,
        framework = RuntimeInformation.FrameworkDescription,
        processors = System.Environment.ProcessorCount,
        arch = RuntimeInformation.ProcessArchitecture.ToString(),
        serverGc = System.Runtime.GCSettings.IsServerGC,
        timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
    };

    private static void Write(string outDir, string name, object payload)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, name), JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private sealed record ThroughputRow(int Lines, int Bytes, string Mode, double DocsPerSecond, double MegabytesPerSecond, double MillisecondsPerDoc, long AllocatedBytesPerDoc, double SlowestSlice, double FastestSlice);
}
