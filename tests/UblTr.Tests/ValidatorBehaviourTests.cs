using System.Text;
using System.Xml.Linq;
using UblTr.SampleGen;
using UblTr.Tests.Support;
using UblTr.Validator;

namespace UblTr.Tests;

public sealed class ValidatorBehaviourTests
{
    [Fact]
    public void Billion_laughs_is_rejected_before_any_expansion()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE lolz [<!ENTITY lol "lol"><!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
            <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">]>
            <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">&lol3;</Invoice>
            """;
        TestAssets.Validate(xml).RuleIds().Should().Equal("XML-002");
    }

    [Fact]
    public void External_entity_pointing_at_a_local_file_is_rejected()
    {
        var secret = Path.GetTempFileName();
        File.WriteAllText(secret, "top-secret");
        var xml = $"""
            <?xml version="1.0"?>
            <!DOCTYPE Invoice [<!ENTITY xxe SYSTEM "file:///{secret.Replace('\\', '/')}">]>
            <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"><Note>&xxe;</Note></Invoice>
            """;

        var result = TestAssets.Validate(xml);

        result.RuleIds().Should().Equal("XML-002");
        result.ToReport(Language.En).Issues.Should().NotContain(i => i.Message.Contains("top-secret"));
        File.Delete(secret);
    }

    [Fact]
    public void Schema_location_in_the_document_is_ignored()
    {
        // A document must not be able to pick its own (lenient or remote) schema.
        var doc = InvoiceWriter.ToXml(SampleCatalog.Basic);
        doc.Root!.SetAttributeValue(XName.Get("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance"),
            "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2 http://127.0.0.1:9/evil.xsd");
        doc.Root.Element(Ns.Cbc + "LineCountNumeric")!.Remove();

        TestAssets.Validate(InvoiceWriter.Serialize(doc)).RuleIds().Should().Equal("XSD-001");
    }

    [Fact]
    public void Rules_still_run_when_the_schema_is_skipped()
    {
        var xml = File.ReadAllText(Path.Combine(TestAssets.RepoRoot, "samples", "invoices", "invalid", "XSD-001.xml"));
        var result = TestAssets.Validate(xml, TestAssets.Options with { SkipSchema = true });
        result.SchemaValidated.Should().BeFalse();
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Rules_only_assets_use_the_builtin_code_list_snapshot()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(InvoiceWriter.ToXmlString(SampleCatalog.Basic)));
        var result = new InvoiceValidator(GibAssets.RulesOnly).Validate(stream, TestAssets.Options);
        result.IsValid.Should().BeTrue();
        result.SchemaValidated.Should().BeFalse();
        result.CodeListSource.Should().Contain("snapshot");
    }

    [Fact]
    public void Disabled_rules_are_suppressed()
    {
        var xml = File.ReadAllText(Path.Combine(TestAssets.RepoRoot, "samples", "invoices", "invalid", "PTY-003.xml"));
        var options = TestAssets.Options with { DisabledRules = new HashSet<string>(["pty-003"], StringComparer.OrdinalIgnoreCase) };
        TestAssets.Validate(xml, options).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_systematic_error_is_capped_per_rule_and_the_drop_is_reported()
    {
        var spec = SyntheticData.RandomInvoice(7, 300) with { Currency = "TRY", ExchangeRate = null };
        var doc = InvoiceWriter.ToXml(spec);
        foreach (var amount in doc.Root!.Elements(Ns.Cac + "InvoiceLine").Elements(Ns.Cbc + "LineExtensionAmount"))
        {
            amount.SetAttributeValue("currencyID", "EUR");
        }

        var result = TestAssets.Validate(InvoiceWriter.Serialize(doc), TestAssets.Options with { MaxFindingsPerRule = 25 });

        result.Findings.Count(f => f.Rule.Id == "CUR-005").Should().Be(25);
        result.DroppedFindings.Should().Be(275);
    }

    [Fact]
    public void Envelope_gets_a_hint_to_validate_the_inner_invoice()
    {
        var xml = File.ReadAllText(Path.Combine(TestAssets.RepoRoot, "samples", "invoices", "invalid", "DOC-001.xml"));
        var issue = TestAssets.Validate(xml).ToReport(Language.En).Issues.Single();
        issue.Message.Should().Contain("StandardBusinessDocument envelope");
    }

    [Fact]
    public void Earchive_kind_rejects_an_efatura_scenario()
    {
        var result = TestAssets.Validate(SampleCatalog.Basic, TestAssets.Options with { Kind = DocumentKind.EArsiv });
        result.RuleIds().Should().Equal("HDR-003");
        result.Document!.Kind.Should().Be(DocumentKind.EArsiv);
    }

    [Fact]
    public void Auto_kind_follows_the_profile()
    {
        TestAssets.Validate(SampleCatalog.EArchive).Document!.Kind.Should().Be(DocumentKind.EArsiv);
        TestAssets.Validate(SampleCatalog.Basic).Document!.Kind.Should().Be(DocumentKind.EFatura);
    }

    [Fact]
    public void Non_charged_tax_subtotal_is_excluded_from_totals()
    {
        // GIB marks computed-but-not-charged VAT (investment incentive exemptions) with CalculationSequenceNumeric -1.
        var doc = InvoiceWriter.ToXml(SampleCatalog.NoLineTaxes);
        var taxTotal = doc.Root!.Element(Ns.Cac + "TaxTotal")!;
        var extra = new XElement(Ns.Cac + "TaxSubtotal",
            new XElement(Ns.Cbc + "TaxableAmount", new XAttribute("currencyID", "TRY"), "1000.00"),
            new XElement(Ns.Cbc + "TaxAmount", new XAttribute("currencyID", "TRY"), "200.00"),
            new XElement(Ns.Cbc + "CalculationSequenceNumeric", "-1"),
            new XElement(Ns.Cbc + "Percent", "20"),
            new XElement(Ns.Cac + "TaxCategory",
                new XElement(Ns.Cbc + "TaxExemptionReasonCode", "351"),
                new XElement(Ns.Cbc + "TaxExemptionReason", "Diğer"),
                new XElement(Ns.Cac + "TaxScheme", new XElement(Ns.Cbc + "TaxTypeCode", "0015"))));
        taxTotal.Add(extra);

        TestAssets.Validate(InvoiceWriter.Serialize(doc)).Findings.Should().BeEmpty();
    }

    [Fact]
    public void Withholding_invoice_accepts_the_net_tax_total_used_by_gib_samples()
    {
        var doc = InvoiceWriter.ToXml(SampleCatalog.Withholding);
        var root = doc.Root!;
        var withheld = decimal.Parse(root.Element(Ns.Cac + "WithholdingTaxTotal")!.Element(Ns.Cbc + "TaxAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture);
        var taxAmount = root.Element(Ns.Cac + "TaxTotal")!.Element(Ns.Cbc + "TaxAmount")!;
        taxAmount.Value = (decimal.Parse(taxAmount.Value, System.Globalization.CultureInfo.InvariantCulture) - withheld)
            .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        TestAssets.Validate(InvoiceWriter.Serialize(doc)).Findings.Should().BeEmpty();
    }

    [Fact]
    public void Base_quantity_divides_the_price()
    {
        var doc = InvoiceWriter.ToXml(SampleCatalog.Basic with { Lines = [new LineSpec("Kablo", 250m, 12m, 20m)] });
        var price = doc.Root!.Element(Ns.Cac + "InvoiceLine")!.Element(Ns.Cac + "Price")!;
        price.Element(Ns.Cbc + "PriceAmount")!.Value = "1200.00";
        price.Add(new XElement(Ns.Cbc + "BaseQuantity", new XAttribute("unitCode", "C62"), "100"));

        // BaseQuantity is not in GIB's PriceType, so the schema rejects it; the arithmetic itself must still agree.
        TestAssets.Validate(InvoiceWriter.Serialize(doc), TestAssets.Options with { SkipSchema = true }).Findings.Should().BeEmpty();
    }

    [Fact]
    public void Report_is_sorted_by_location_and_localized()
    {
        var doc = InvoiceWriter.ToXml(SampleCatalog.Basic);
        doc.Root!.Element(Ns.Cbc + "UBLVersionID")!.Value = "2.0";
        doc.Root.Element(Ns.Cac + "LegalMonetaryTotal")!.Element(Ns.Cbc + "PayableAmount")!.Value = "1.00";

        var tr = TestAssets.Validate(InvoiceWriter.Serialize(doc)).ToReport(Language.Tr, "x.xml");

        tr.Issues.Select(i => i.RuleId).Should().Equal("HDR-001", "TOT-005");
        tr.Issues[0].Line.Should().BeLessThan(tr.Issues[1].Line!.Value);
        tr.Language.Should().Be("tr");
        tr.Source.Should().Be("x.xml");
    }
}
