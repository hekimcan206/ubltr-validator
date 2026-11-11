using UblTr.Tests.Support;
using UblTr.Validator.CodeLists;

namespace UblTr.Tests;

/// <summary>Tests that need the fetched GIB package (it is downloaded at build time and copied next to the tests).</summary>
public sealed class GibPackageTests
{
    [Fact]
    public void Fetched_package_compiles_into_a_schema_set_and_code_lists()
    {
        TestAssets.Gib.Schemas.Should().NotBeNull();
        TestAssets.Gib.Schemas!.IsCompiled.Should().BeTrue();
        TestAssets.Gib.CodeLists.Source.Should().Contain("UBL-TR_Codelist.xml");
    }

    /// <summary>
    /// Drift detector: the built-in snapshot is only a fallback, but if GIB changes a list and the pin is updated, this
    /// test makes the snapshot follow instead of silently disagreeing with the official file.
    /// </summary>
    [Fact]
    public void Builtin_snapshot_matches_the_fetched_gib_code_lists()
    {
        var official = TestAssets.Gib.CodeLists;
        foreach (var name in CodeListSet.BuiltIn.Names)
        {
            official.Get(name).Should().BeEquivalentTo(CodeListSet.BuiltIn.Get(name), $"list {name}");
        }
    }

    [Fact]
    public void Gib_codelist_parser_reads_every_list_including_units()
    {
        var lists = TestAssets.Gib.CodeLists;
        lists.Has(CodeListSet.UnitCodes).Should().BeTrue();
        lists.Contains(CodeListSet.UnitCodes, "C62").Should().BeTrue();
        lists.Contains(CodeListSet.Currencies, "TRY").Should().BeTrue();
        lists.Contains(CodeListSet.ProfileIds, "EARSIVFATURA").Should().BeFalse("e-Arşiv has its own list");
    }

    /// <summary>
    /// GIB's own sample invoices from the UBL-TR 1.2.1 package. They are demos: most carry placeholder VKNs and some
    /// have arithmetic slips, so this test pins what the validator says about them (see docs/gib-samples.md) rather
    /// than expecting them to be clean. The important properties: the run never throws, the XSD verdict is stable,
    /// and nothing outside the documented findings appears.
    /// </summary>
    [Fact]
    public void Gib_official_samples_produce_the_documented_findings()
    {
        var dir = Directory.GetDirectories(Path.Combine(TestAssets.RepoRoot, ".gib"), "ubltr-*").SingleOrDefault();
        Assert.SkipWhen(dir is null, "GIB package not fetched into .gib/ (build with network access)");
        var files = Directory.GetFiles(Path.Combine(dir!, "UBLTR_1.2.1_Paketi", "xml"), "*.xml");
        var invoices = files.Select(f => (File: Path.GetFileName(f), Result: TestAssets.Validator.Validate(f, TestAssets.Options)))
            .Where(r => r.Result.Document is not null)
            .ToList();

        invoices.Should().HaveCount(28);
        var schemaFailures = invoices.Where(r => r.Result.Findings.Any(f => f.Rule.Id == "XSD-001")).Select(r => r.File).Order();
        schemaFailures.Should().Equal("HKS-Ornek1.xml", "HKS-Ornek2.xml");

        // Identifier checksums aside (GIB demos use placeholder tax numbers), these are the samples whose
        // arithmetic or header is inconsistent. Each one was checked by hand; see docs/gib-samples.md.
        var withOtherErrors = invoices
            .Where(r => r.Result.Findings.Any(f => f.Rule.Severity == Validator.Rules.Severity.Error
                                                   && f.Rule.Id is not ("PTY-002" or "PTY-003" or "PTY-004" or "PTY-005" or "PTY-001" or "PTY-008" or "XSD-001")))
            .Select(r => r.File).Order(StringComparer.Ordinal).ToList();
        withOtherErrors.Should().HaveCount(10);
        invoices.Count(r => r.Result.IsValid).Should().Be(1);
    }
}
