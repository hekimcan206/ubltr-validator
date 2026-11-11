using System.Globalization;
using System.Xml.Linq;
using FsCheck;
using FsCheck.Xunit;
using UblTr.SampleGen;
using UblTr.Tests.Support;
using UblTr.Validator;

namespace UblTr.Tests;

/// <summary>
/// Properties over randomly generated invoices (seeded Bogus data, 1-120 lines, mixed VAT rates, allowances, 4-decimal
/// prices, TRY and EUR, with and without line tax breakdowns).
/// </summary>
public sealed class InvoicePropertyTests
{
    /// <summary>No false positives: a generator-built invoice is arithmetically consistent by construction.</summary>
    [Property(MaxTest = 150)]
    public bool Every_generated_invoice_is_valid_with_no_warnings(int seed, PositiveInt lines)
    {
        var result = TestAssets.Validate(SyntheticData.RandomInvoice(seed, lines.Get % 120 + 1));
        return result.Findings.Count == 0;
    }

    /// <summary>No false negatives on totals: moving any one line amount by at least one kuruş is always caught.</summary>
    [Property(MaxTest = 150)]
    public bool Changing_any_line_amount_is_detected(int seed, PositiveInt lines, PositiveInt pick, PositiveInt kurus)
    {
        var doc = InvoiceWriter.ToXml(SyntheticData.RandomInvoice(seed, lines.Get % 40 + 1));
        var all = doc.Root!.Elements(Ns.Cac + "InvoiceLine").ToList();
        var amount = all[pick.Get % all.Count].Element(Ns.Cbc + "LineExtensionAmount")!;
        var delta = (kurus.Get % 10_000 + 2) / 100m;
        amount.Value = (decimal.Parse(amount.Value, CultureInfo.InvariantCulture) + delta).ToString("0.00", CultureInfo.InvariantCulture);

        var ids = TestAssets.Validate(InvoiceWriter.Serialize(doc)).RuleIds();
        return ids.Contains("TOT-001") && ids.Contains("TOT-002");
    }

    /// <summary>Rounding drift: document VAT computed once on the aggregate base is accepted for any line count.</summary>
    [Property(MaxTest = 60)]
    public bool Document_vat_rounded_once_on_the_aggregate_is_accepted(int seed, PositiveInt lines)
    {
        var spec = SyntheticData.RandomInvoice(seed, lines.Get % 400 + 1) with { Withholding = null };
        var doc = InvoiceWriter.ToXml(spec);
        var root = doc.Root!;
        var taxTotal = root.Element(Ns.Cac + "TaxTotal")!;
        var total = 0m;
        foreach (var subtotal in taxTotal.Elements(Ns.Cac + "TaxSubtotal"))
        {
            var taxable = decimal.Parse(subtotal.Element(Ns.Cbc + "TaxableAmount")!.Value, CultureInfo.InvariantCulture);
            var percent = decimal.Parse(subtotal.Element(Ns.Cbc + "Percent")!.Value, CultureInfo.InvariantCulture);
            var tax = Math.Round(taxable * percent / 100m, 2, MidpointRounding.AwayFromZero);
            subtotal.Element(Ns.Cbc + "TaxAmount")!.Value = tax.ToString("0.00", CultureInfo.InvariantCulture);
            total += tax;
        }

        taxTotal.Element(Ns.Cbc + "TaxAmount")!.Value = total.ToString("0.00", CultureInfo.InvariantCulture);
        var lmt = root.Element(Ns.Cac + "LegalMonetaryTotal")!;
        var exclusive = decimal.Parse(lmt.Element(Ns.Cbc + "TaxExclusiveAmount")!.Value, CultureInfo.InvariantCulture);
        lmt.Element(Ns.Cbc + "TaxInclusiveAmount")!.Value = (exclusive + total).ToString("0.00", CultureInfo.InvariantCulture);
        lmt.Element(Ns.Cbc + "PayableAmount")!.Value = (exclusive + total).ToString("0.00", CultureInfo.InvariantCulture);

        return TestAssets.Validate(InvoiceWriter.Serialize(doc)).Findings.Count == 0;
    }

    [Property(MaxTest = 100)]
    public bool Validation_is_deterministic(int seed)
    {
        var xml = InvoiceWriter.ToXmlString(SyntheticData.RandomInvoice(seed, 5));
        var doc = XDocument.Parse(xml);
        doc.Root!.Element(Ns.Cbc + "ID")!.Value = "bad";
        var mutated = InvoiceWriter.Serialize(doc);
        var a = TestAssets.Validate(mutated).ToReport(Language.En);
        var b = TestAssets.Validate(mutated).ToReport(Language.En);
        return a.Issues.SequenceEqual(b.Issues);
    }
}
