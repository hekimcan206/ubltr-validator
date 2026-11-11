using System.Globalization;
using System.Text;
using System.Text.Json;
using UblTr.SampleGen;

// Usage:
//   samples <dir>                          committed golden samples (valid/ and invalid/) plus catalog.json
//   large <dir> <lines,lines,...>          one valid invoice per line count, for throughput/memory benchmarks
//   random <dir> <count> <lines> [seed]    random valid invoices
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: samples <dir> | large <dir> <lines,...> | random <dir> <count> <lines> [seed]");
    return 2;
}

var dir = Path.GetFullPath(args[1]);
switch (args[0])
{
    case "samples":
        Write(Path.Combine(dir, "valid"), SampleCatalog.Valid);
        Write(Path.Combine(dir, "invalid"), SampleCatalog.Invalid);
        var catalog = SampleCatalog.Valid.Select(s => new { file = $"valid/{s.Name}.xml", s.Description, expectedRules = s.ExpectedRules })
            .Concat(SampleCatalog.Invalid.Select(s => new { file = $"invalid/{s.Name}.xml", s.Description, expectedRules = s.ExpectedRules }));
        File.WriteAllText(Path.Combine(dir, "catalog.json"),
            JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
        Console.WriteLine($"wrote {SampleCatalog.Valid.Count} valid and {SampleCatalog.Invalid.Count} invalid samples to {dir}");
        return 0;

    case "large":
        Directory.CreateDirectory(dir);
        foreach (var lines in args[2].Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)))
        {
            var spec = SyntheticData.RandomInvoice(seed: lines, lineCount: lines) with { LineTaxTotals = true };
            var path = Path.Combine(dir, $"invoice-{lines}-lines.xml");
            File.WriteAllText(path, InvoiceWriter.ToXmlString(spec), new UTF8Encoding(false));
            Console.WriteLine($"{path} {new FileInfo(path).Length:N0} bytes");
        }

        return 0;

    case "random":
        Directory.CreateDirectory(dir);
        var count = int.Parse(args[2], CultureInfo.InvariantCulture);
        var lineCount = int.Parse(args[3], CultureInfo.InvariantCulture);
        var seed = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1;
        for (var i = 0; i < count; i++)
        {
            File.WriteAllText(Path.Combine(dir, $"random-{seed + i:D5}.xml"),
                InvoiceWriter.ToXmlString(SyntheticData.RandomInvoice(seed + i, lineCount)), new UTF8Encoding(false));
        }

        Console.WriteLine($"wrote {count} random invoices to {dir}");
        return 0;

    default:
        Console.Error.WriteLine($"unknown command '{args[0]}'");
        return 2;
}

static void Write(string dir, IEnumerable<Sample> samples)
{
    Directory.CreateDirectory(dir);
    foreach (var sample in samples)
    {
        File.WriteAllText(Path.Combine(dir, sample.Name + ".xml"), sample.Xml(), new UTF8Encoding(false));
    }
}
