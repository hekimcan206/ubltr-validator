using System.Text;
using BenchmarkDotNet.Attributes;
using UblTr.SampleGen;
using UblTr.Validator;

namespace UblTr.Benchmarks;

/// <summary>Statistically rigorous numbers (with allocations) for typical small and medium invoices.</summary>
[MemoryDiagnoser]
public class ValidationBenchmarks
{
    private InvoiceValidator _validator = null!;
    private byte[] _document = null!;

    [Params(10, 100)]
    public int Lines { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _validator = new InvoiceValidator(GibAssets.LoadDefault());
        _document = Encoding.UTF8.GetBytes(InvoiceWriter.ToXmlString(SyntheticData.RandomInvoice(Lines, Lines) with { LineTaxTotals = true }));
    }

    [Benchmark(Baseline = true)]
    public ValidationResult Full() => _validator.Validate(new MemoryStream(_document));

    [Benchmark]
    public ValidationResult XsdOnly() => _validator.Validate(new MemoryStream(_document), new ValidationOptions { SkipRules = true });

    [Benchmark]
    public ValidationResult RulesOnly() => _validator.Validate(new MemoryStream(_document), new ValidationOptions { SkipSchema = true });
}

/// <summary>
/// Why the rules use pre-built XNames: the cost of one child lookup on a typical invoice line, with the name built from
/// a string on every call (what the first version did) versus a cached XName instance.
/// </summary>
[MemoryDiagnoser]
public class XNameLookupBenchmarks
{
    private static readonly System.Xml.Linq.XName Cached = Ns.Cbc + "LineExtensionAmount";
    private System.Xml.Linq.XElement _line = null!;

    [GlobalSetup]
    public void Setup()
    {
        var doc = InvoiceWriter.ToXml(SyntheticData.RandomInvoice(1, 1) with { LineTaxTotals = true });
        _line = doc.Root!.Element(Ns.Cac + "InvoiceLine")!;
    }

    [Benchmark(Baseline = true)]
    public System.Xml.Linq.XElement? BuiltFromString() => _line.Element(Ns.Cbc + "LineExtensionAmount");

    [Benchmark]
    public System.Xml.Linq.XElement? CachedInstance() => _line.Element(Cached);
}
