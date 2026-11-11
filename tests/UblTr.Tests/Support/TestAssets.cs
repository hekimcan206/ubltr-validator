using UblTr.SampleGen;
using UblTr.Validator;

namespace UblTr.Tests.Support;

/// <summary>Shared, lazily loaded validator state: compiling the UBL-TR XSDs once per test run keeps the suite fast.</summary>
internal static class TestAssets
{
    private static readonly Lazy<GibAssets> LazyAssets = new(() =>
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "gib");
        if (!Directory.Exists(Path.Combine(dir, "xsdrt")))
        {
            throw new InvalidOperationException(
                "GIB schemas were not copied next to the test binaries. Build with network access (the build downloads them) or set -p:GibCacheDir.");
        }

        return GibAssets.Load(dir);
    });

    public static GibAssets Gib => LazyAssets.Value;

    private static readonly Lazy<InvoiceValidator> LazyValidator = new(() => new InvoiceValidator(Gib));

    public static InvoiceValidator Validator => LazyValidator.Value;

    public static ValidationOptions Options { get; } = new() { Today = SampleCatalog.Today };

    public static string RepoRoot { get; } = FindRepoRoot();

    public static ValidationResult Validate(string xml, ValidationOptions? options = null)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return Validator.Validate(stream, options ?? Options);
    }

    public static ValidationResult Validate(InvoiceSpec spec, ValidationOptions? options = null) =>
        Validate(InvoiceWriter.ToXmlString(spec), options);

    public static string[] RuleIds(this ValidationResult result) => [.. result.Findings.Select(f => f.Rule.Id).Distinct().Order(StringComparer.Ordinal)];

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "UblTr.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (UblTr.slnx) not found.");
    }
}
