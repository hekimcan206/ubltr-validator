using System.Text;
using System.Text.Json;
using UblTr.SampleGen;
using UblTr.Tests.Support;
using UblTr.Validator;

namespace UblTr.Tests;

/// <summary>
/// Golden tests over the committed samples. For every sample: the committed XML is what the generator produces today
/// (so samples cannot drift from the code), the validator reports exactly the expected rule ids, and the rendered
/// findings (rule, location, Turkish and English message and fix) match the committed expectation file.
/// Set UBLTR_UPDATE_GOLDEN=1 to rewrite the expectation files after an intended change, then review the diff.
/// </summary>
public sealed class GoldenSampleTests
{
    private static readonly bool Update = Environment.GetEnvironmentVariable("UBLTR_UPDATE_GOLDEN") == "1";

    public static TheoryData<string> ValidNames => [.. SampleCatalog.Valid.Select(s => s.Name)];

    public static TheoryData<string> InvalidNames => [.. SampleCatalog.Invalid.Select(s => s.Name)];

    [Theory]
    [MemberData(nameof(ValidNames))]
    public void Valid_sample_has_no_findings_at_all(string name)
    {
        var sample = SampleCatalog.Valid.Single(s => s.Name == name);
        var xml = AssertCommitted("valid", sample);

        var result = TestAssets.Validate(xml);

        result.SchemaValidated.Should().BeTrue();
        result.Findings.Should().BeEmpty(because: string.Join("; ", result.Findings.Select(f => f.Localize(Language.En).Message)));
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Invalid_sample_reports_exactly_its_rule(string name)
    {
        var sample = SampleCatalog.Invalid.Single(s => s.Name == name);
        var xml = AssertCommitted("invalid", sample);

        var result = TestAssets.Validate(xml);

        result.RuleIds().Should().Equal(sample.ExpectedRules);
        var rule = result.Findings[0].Rule;
        result.IsValid.Should().Be(rule.Severity == Validator.Rules.Severity.Warning);
        AssertGolden(name, result);
    }

    [Fact]
    public void Every_rule_in_the_catalog_has_an_invalid_golden_sample()
    {
        var covered = SampleCatalog.Invalid.SelectMany(s => s.ExpectedRules).ToHashSet();
        Validator.Rules.RuleCatalog.All.Select(r => r.Id).Where(id => !covered.Contains(id)).Should().BeEmpty();
    }

    [Fact]
    public void Committed_catalog_lists_every_sample()
    {
        var catalog = File.ReadAllText(Path.Combine(TestAssets.RepoRoot, "samples", "invoices", "catalog.json"));
        foreach (var sample in SampleCatalog.Valid.Concat(SampleCatalog.Invalid))
        {
            catalog.Should().Contain($"{sample.Name}.xml");
        }
    }

    private static string AssertCommitted(string folder, Sample sample)
    {
        var path = Path.Combine(TestAssets.RepoRoot, "samples", "invoices", folder, sample.Name + ".xml");
        var generated = sample.Xml();
        if (Update)
        {
            File.WriteAllText(path, generated, new UTF8Encoding(false));
        }

        File.Exists(path).Should().BeTrue($"{path} must be committed (run the generator: dotnet run --project tools/UblTr.SampleGen -- samples samples/invoices)");
        Normalize(File.ReadAllText(path)).Should().Be(Normalize(generated), "the committed sample must match the generator");
        return generated;
    }

    private static void AssertGolden(string name, ValidationResult result)
    {
        var tr = result.ToReport(Language.Tr).Issues;
        var en = result.ToReport(Language.En).Issues;
        var expected = tr.Zip(en, (t, e) => new GoldenIssue(t.RuleId, t.Severity.ToString(), t.Line, t.Column, t.Path, t.Message, e.Message, t.Fix, e.Fix));
        var json = JsonSerializer.Serialize(expected, ReportJson.Options) + "\n";
        var path = Path.Combine(TestAssets.RepoRoot, "samples", "invoices", "expected", name + ".json");
        if (Update)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        Normalize(File.ReadAllText(path)).Should().Be(Normalize(json), $"golden file {name}.json is out of date (UBLTR_UPDATE_GOLDEN=1 rewrites it)");
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private sealed record GoldenIssue(
        string RuleId, string Severity, int? Line, int? Column, string? Path, string MessageTr, string MessageEn, string FixTr, string FixEn);
}
