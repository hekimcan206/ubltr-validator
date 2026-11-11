using System.Globalization;
using System.Xml.Linq;
using UblTr.Validator;

namespace UblTr.SampleGen;

/// <summary>A committed sample: the XML it produces and the exact rule ids the validator must report for it.</summary>
public sealed record Sample(string Name, string Description, Func<string> Xml, IReadOnlyList<string> ExpectedRules);

/// <summary>
/// Valid synthetic invoices plus one invalid sample per rule. Every invalid sample is a valid one with a single,
/// targeted change, and expects exactly one rule id: a mutation that trips a second rule is a bug in the mutation or
/// in a rule, and the golden test says so.
/// </summary>
public static class SampleCatalog
{
    private static readonly XNamespace Cac = Ns.Cac;
    private static readonly XNamespace Cbc = Ns.Cbc;

    /// <summary>The fixed "today" golden tests run with, so date rules do not depend on the wall clock.</summary>
    public static readonly DateOnly Today = new(2026, 11, 11);

    public static InvoiceSpec Basic => new()
    {
        Id = "SYN2026000000001",
        Supplier = SyntheticData.Supplier,
        Customer = SyntheticData.CompanyCustomer,
        Note = "Sentetik örnek fatura / synthetic sample invoice",
        Lines =
        [
            new LineSpec("Yazılım lisansı (yıllık)", 3m, 1250.50m, 20m),
            new LineSpec("Danışmanlık hizmeti", 7.5m, 980m, 20m, Allowance: 367.50m),
            new LineSpec("Teknik kitap", 4m, 312.4567m, 10m),
            new LineSpec("Ekmek", 25m, 12.5m, 1m),
        ],
    };

    public static InvoiceSpec Commercial => Basic with { Id = "SYN2026000000002", Profile = "TICARIFATURA", Uuid = Guid.Parse("0b9f7c64-41a2-4f0e-9d65-2b3c7e1a8f10") };

    public static InvoiceSpec ForeignCurrency => Basic with
    {
        Id = "EXP2026000000003",
        Profile = "TICARIFATURA",
        Currency = "USD",
        ExchangeRate = 34.123456m,
        Uuid = Guid.Parse("7d1e4b2a-9c3f-4e8d-a1b2-c3d4e5f60718"),
    };

    public static InvoiceSpec EArchive => Basic with
    {
        Id = "EAR2026000000004",
        Profile = "EARSIVFATURA",
        Customer = SyntheticData.PersonCustomer,
        Uuid = Guid.Parse("c2a3b4d5-e6f7-4819-8a7b-6c5d4e3f2a1b"),
    };

    public static InvoiceSpec Withholding => Basic with
    {
        Id = "TEV2026000000005",
        Type = "TEVKIFAT",
        Uuid = Guid.Parse("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"),
        Lines = [new LineSpec("Temizlik hizmeti (Kasım)", 1m, 20000m, 20m), new LineSpec("Temizlik malzemesi bedeli", 10m, 150m, 20m)],
        Withholding = new WithholdingSpec("603", 70m),
    };

    public static InvoiceSpec Exempt => Basic with
    {
        Id = "IST2026000000006",
        Type = "ISTISNA",
        Uuid = Guid.Parse("9e8d7c6b-5a49-4382-a1f0-e9d8c7b6a594"),
        Exemption = new ExemptionSpec("301", "11/1-a Mal ihracatı"),
    };

    public static InvoiceSpec Return => Basic with
    {
        Id = "IAD2026000000007",
        Type = "IADE",
        Uuid = Guid.Parse("f0e1d2c3-b4a5-4697-8879-6a5b4c3d2e1f"),
        ReturnedInvoiceId = "SYN2026000000001",
        Lines = [new LineSpec("Yazılım lisansı (yıllık) - iade", 1m, 1250.50m, 20m)],
    };

    /// <summary>Same as <see cref="Basic"/> but without line-level TaxTotal, for mutations of the document breakdown.</summary>
    public static InvoiceSpec NoLineTaxes => Basic with { LineTaxTotals = false };

    public static IReadOnlyList<Sample> Valid { get; } =
    [
        V("temel-satis-try", "TEMELFATURA / SATIS in TRY, three VAT rates, a line allowance, line tax breakdowns", Basic),
        V("ticari-satis-try", "TICARIFATURA / SATIS in TRY", Commercial),
        V("ticari-satis-usd", "TICARIFATURA / SATIS in USD with PricingExchangeRate", ForeignCurrency),
        V("earsiv-satis-tckn", "EARSIVFATURA / SATIS to a private person (TCKN)", EArchive),
        V("temel-tevkifat", "TEMELFATURA / TEVKIFAT, withholding code 603 at 70 %", Withholding),
        V("temel-istisna", "TEMELFATURA / ISTISNA, exemption code 301 (export of goods)", Exempt),
        V("temel-iade", "TEMELFATURA / IADE referencing the returned invoice", Return),
        V("temel-satis-no-line-taxes", "TEMELFATURA / SATIS without line-level TaxTotal (allowed by UBL-TR)", NoLineTaxes),
    ];

    public static IReadOnlyList<Sample> Invalid { get; } =
    [
        T("XML-001", "unclosed element", Basic, xml => xml.Replace("</cbc:Note>", "", StringComparison.Ordinal)),
        T("XML-002", "DOCTYPE with an internal entity", Basic, xml => xml.Replace("<Invoice ",
            "<!DOCTYPE Invoice [<!ENTITY x \"expanded\">]>\n<Invoice ", StringComparison.Ordinal)),
        X("DOC-001", "invoice wrapped in a StandardBusinessDocument envelope", Basic, d =>
        {
            XNamespace sh = Ns.Sbd;
            var invoice = d.Root!;
            invoice.Remove();
            d.Add(new XElement(sh + "StandardBusinessDocument", new XElement(sh + "StandardBusinessDocumentHeader"), invoice));
        }),
        X("XSD-001", "mandatory cbc:LineCountNumeric removed", Basic, d => E(d, "LineCountNumeric").Remove()),

        X("HDR-001", "UBLVersionID 2.0", Basic, d => E(d, "UBLVersionID").Value = "2.0"),
        X("HDR-002", "CustomizationID TR1.0", Basic, d => E(d, "CustomizationID").Value = "TR1.0"),
        X("HDR-003", "unknown scenario TEMEL", Basic, d => E(d, "ProfileID").Value = "TEMEL"),
        X("HDR-004", "unknown invoice type SATISS", Basic, d => E(d, "InvoiceTypeCode").Value = "SATISS"),
        X("HDR-005", "IADE under TICARIFATURA", Return, d => E(d, "ProfileID").Value = "TICARIFATURA"),
        X("HDR-006", "ENERJI scenario with type SATIS", Basic, d => E(d, "ProfileID").Value = "ENERJI"),
        X("HDR-007", "TEKNOLOJIDESTEK under TEMELFATURA", Basic, d => E(d, "InvoiceTypeCode").Value = "TEKNOLOJIDESTEK"),
        X("HDR-008", "IDIS scenario with type KOMISYONCU", Basic, d =>
        {
            E(d, "ProfileID").Value = "IDIS";
            E(d, "InvoiceTypeCode").Value = "KOMISYONCU";
        }),
        X("HDR-009", "YTBSATIS on an e-Fatura (TEMELFATURA)", Basic, d => E(d, "InvoiceTypeCode").Value = "YTBSATIS"),
        X("HDR-010", "two-character series prefix", Basic, d => E(d, "ID").Value = "SY2026000000001"),
        X("HDR-011", "number from the 2025 sequence on a 2026 invoice (warning)", Basic, d => E(d, "ID").Value = "SYN2025000000001"),
        X("HDR-012", "ETTN is not a UUID", Basic, d => E(d, "UUID").Value = "SYN-2026-0001"),
        X("HDR-013", "CopyIndicator true", Basic, d => E(d, "CopyIndicator").Value = "true"),
        X("HDR-014", "future issue date (warning)", Basic, d =>
        {
            E(d, "IssueDate").Value = "2027-01-05";
            E(d, "ID").Value = "SYN2027000000001";
        }),
        X("HDR-015", "LineCountNumeric says 5, invoice has 4 lines", Basic, d => E(d, "LineCountNumeric").Value = "5"),
        X("RET-001", "IADE without a BillingReference", Return, d => d.Root!.Element(Cac + "BillingReference")!.Remove()),

        X("PTY-001", "supplier identified only by MERSIS number", Basic, d =>
        {
            var id = SupplierParty(d).Element(Cac + "PartyIdentification")!.Element(Cbc + "ID")!;
            id.SetAttributeValue("schemeID", "MERSISNO");
            id.Value = "0123456789000015";
        }),
        X("PTY-002", "customer VKN has 9 digits", Basic, d => CustomerId(d).Value = "987654321"),
        X("PTY-003", "customer VKN check digit off by one", Basic, d => CustomerId(d).Value = Bump(CustomerId(d).Value)),
        X("PTY-004", "customer TCKN has 10 digits", EArchive, d => CustomerId(d).Value = CustomerId(d).Value[..10]),
        X("PTY-005", "customer TCKN check digits wrong", EArchive, d => CustomerId(d).Value = Bump(CustomerId(d).Value)),
        X("PTY-006", "VKN customer without PartyName", Basic, d => CustomerParty(d).Element(Cac + "PartyName")!.Remove()),
        X("PTY-007", "TCKN customer without Person", EArchive, d => CustomerParty(d).Element(Cac + "Person")!.Remove()),
        X("PTY-008", "extra identifier with unknown schemeID", Basic, d => CustomerParty(d).Element(Cac + "PartyIdentification")!
            .AddAfterSelf(new XElement(Cac + "PartyIdentification", new XElement(Cbc + "ID", new XAttribute("schemeID", "SICILNO"), "12345")))),

        X("CUR-001", "TaxCurrencyCode 'TL'", Basic, d => E(d, "DocumentCurrencyCode").AddAfterSelf(new XElement(Cbc + "TaxCurrencyCode", "TL"))),
        X("CUR-002", "USD invoice without PricingExchangeRate", ForeignCurrency, d => d.Root!.Element(Cac + "PricingExchangeRate")!.Remove()),
        X("CUR-003", "exchange rate with 7 decimals", ForeignCurrency, d =>
            d.Root!.Element(Cac + "PricingExchangeRate")!.Element(Cbc + "CalculationRate")!.Value = "34.1234567"),
        X("CUR-004", "exchange rate targets EUR instead of TRY", ForeignCurrency, d =>
            d.Root!.Element(Cac + "PricingExchangeRate")!.Element(Cbc + "TargetCurrencyCode")!.Value = "EUR"),
        X("CUR-005", "one line amount in EUR on a TRY invoice", Basic, d =>
            Lines(d)[0].Element(Cbc + "LineExtensionAmount")!.SetAttributeValue("currencyID", "EUR")),

        X("TOT-001", "line 1 price changed, amount kept", Basic, d => Lines(d)[0].Element(Cac + "Price")!.Element(Cbc + "PriceAmount")!.Value = "1300.00"),
        X("TOT-002", "LegalMonetaryTotal line total +10 (allowance total added so TaxExclusive still adds up)", Basic, d =>
        {
            var lmt = Lmt(d);
            Add(lmt.Element(Cbc + "LineExtensionAmount")!, 10m);
            lmt.Element(Cbc + "TaxInclusiveAmount")!.AddAfterSelf(new XElement(Cbc + "AllowanceTotalAmount", new XAttribute("currencyID", "TRY"), "10.00"));
        }),
        X("TOT-003", "TaxExclusiveAmount +5 (and the totals after it)", Basic, d =>
        {
            var lmt = Lmt(d);
            Add(lmt.Element(Cbc + "TaxExclusiveAmount")!, 5m);
            Add(lmt.Element(Cbc + "TaxInclusiveAmount")!, 5m);
            Add(lmt.Element(Cbc + "PayableAmount")!, 5m);
        }),
        X("TOT-004", "TaxInclusiveAmount +5 (and PayableAmount)", Basic, d =>
        {
            Add(Lmt(d).Element(Cbc + "TaxInclusiveAmount")!, 5m);
            Add(Lmt(d).Element(Cbc + "PayableAmount")!, 5m);
        }),
        X("TOT-005", "PayableAmount +5", Basic, d => Add(Lmt(d).Element(Cbc + "PayableAmount")!, 5m)),
        X("TOT-006", "PayableAmount with three decimals", Basic, d => Lmt(d).Element(Cbc + "PayableAmount")!.Value += "4"),

        X("TAX-001", "document TaxTotal/TaxAmount +3", Basic, d => Add(d.Root!.Element(Cac + "TaxTotal")!.Element(Cbc + "TaxAmount")!, 3m)),
        X("TAX-002", "document 20 % subtotal labelled 18 %", NoLineTaxes, d => DocSubtotals(d)[0].Element(Cbc + "Percent")!.Value = "18"),
        X("TAX-003", "tax type code 9999", NoLineTaxes, d => SubtotalCode(DocSubtotals(d)[0]).Value = "9999"),
        X("TAX-004", "line 1 taxable and tax raised consistently, document breakdown unchanged", Basic, d =>
        {
            var lineTax = Lines(d)[0].Element(Cac + "TaxTotal")!;
            Add(lineTax.Element(Cbc + "TaxAmount")!, 2m);
            var subtotal = lineTax.Element(Cac + "TaxSubtotal")!;
            Add(subtotal.Element(Cbc + "TaxableAmount")!, 10m);
            Add(subtotal.Element(Cbc + "TaxAmount")!, 2m);
        }),
        X("TAX-005", "zero VAT on ISTISNA without exemption reason", Exempt, d =>
        {
            foreach (var category in d.Root!.Element(Cac + "TaxTotal")!.Descendants(Cac + "TaxCategory"))
            {
                category.Element(Cbc + "TaxExemptionReasonCode")?.Remove();
                category.Element(Cbc + "TaxExemptionReason")?.Remove();
            }
        }),
        X("TAX-006", "exemption code 999", Exempt, d => DocSubtotals(d)[0].Element(Cac + "TaxCategory")!.Element(Cbc + "TaxExemptionReasonCode")!.Value = "999"),
        X("TAX-007", "exemption code 301 on a SATIS invoice", Exempt, d => E(d, "InvoiceTypeCode").Value = "SATIS"),
        X("TAX-008", "withholding on a SATIS invoice", Withholding, d => E(d, "InvoiceTypeCode").Value = "SATIS"),
        X("TAX-009", "withholding code 603 with 40 % (603 is defined at 70 %)", Withholding, d =>
            d.Root!.Element(Cac + "WithholdingTaxTotal")!.Element(Cac + "TaxSubtotal")!.Element(Cbc + "Percent")!.Value = "40"),
        X("TAX-010", "tax code 4171 on a SATIS invoice", NoLineTaxes, d => SubtotalCode(DocSubtotals(d)[0]).Value = "4171"),
    ];

    private static Sample V(string name, string description, InvoiceSpec spec) =>
        new(name, description, () => InvoiceWriter.ToXmlString(spec), []);

    private static Sample X(string rule, string description, InvoiceSpec spec, Action<XDocument> mutate) =>
        new(rule, description, () =>
        {
            var doc = InvoiceWriter.ToXml(spec);
            mutate(doc);
            return InvoiceWriter.Serialize(doc);
        }, [rule]);

    private static Sample T(string rule, string description, InvoiceSpec spec, Func<string, string> mutate) =>
        new(rule, description, () => mutate(InvoiceWriter.ToXmlString(spec)), [rule]);

    private static XElement E(XDocument d, string cbcName) => d.Root!.Element(Cbc + cbcName)!;

    private static XElement Lmt(XDocument d) => d.Root!.Element(Cac + "LegalMonetaryTotal")!;

    private static List<XElement> Lines(XDocument d) => [.. d.Root!.Elements(Cac + "InvoiceLine")];

    private static List<XElement> DocSubtotals(XDocument d) => [.. d.Root!.Element(Cac + "TaxTotal")!.Elements(Cac + "TaxSubtotal")];

    private static XElement SubtotalCode(XElement subtotal) =>
        subtotal.Element(Cac + "TaxCategory")!.Element(Cac + "TaxScheme")!.Element(Cbc + "TaxTypeCode")!;

    private static XElement SupplierParty(XDocument d) => d.Root!.Element(Cac + "AccountingSupplierParty")!.Element(Cac + "Party")!;

    private static XElement CustomerParty(XDocument d) => d.Root!.Element(Cac + "AccountingCustomerParty")!.Element(Cac + "Party")!;

    private static XElement CustomerId(XDocument d) => CustomerParty(d).Element(Cac + "PartyIdentification")!.Element(Cbc + "ID")!;

    private static void Add(XElement amount, decimal delta) =>
        amount.Value = (decimal.Parse(amount.Value, CultureInfo.InvariantCulture) + delta).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Bump(string digits) =>
        digits[..^1] + (char)('0' + (digits[^1] - '0' + 1) % 10);
}
