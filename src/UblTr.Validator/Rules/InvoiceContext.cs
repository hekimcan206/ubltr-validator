using System.Globalization;
using System.Xml.Linq;
using UblTr.Validator.CodeLists;

namespace UblTr.Validator.Rules;

/// <summary>
/// Read-only view of one parsed invoice shared by all checks. Header values and the line list are read once here
/// instead of being re-queried by every rule.
/// </summary>
public sealed class InvoiceContext
{
    private readonly List<Finding> _findings;

    internal InvoiceContext(XElement root, ValidationOptions options, CodeListSet codeLists, List<Finding> findings)
    {
        Root = root;
        Options = options;
        CodeLists = codeLists;
        _findings = findings;

        ProfileId = Text(root, Ns.Cbc + "ProfileID");
        InvoiceTypeCode = Text(root, Ns.Cbc + "InvoiceTypeCode");
        Currency = Text(root, Ns.Cbc + "DocumentCurrencyCode");
        Lines = [.. root.Elements(Ns.Cac + "InvoiceLine")];
        HasWithholding = root.Element(Ns.Cac + "WithholdingTaxTotal") is not null;
        Kind = options.Kind switch
        {
            DocumentKind.Auto => ProfileId == "EARSIVFATURA" ? DocumentKind.EArsiv : DocumentKind.EFatura,
            var explicitKind => explicitKind,
        };
    }

    public XElement Root { get; }

    public ValidationOptions Options { get; }

    public CodeListSet CodeLists { get; }

    /// <summary>The regime actually applied (Auto resolved from the ProfileID).</summary>
    public DocumentKind Kind { get; }

    public string? ProfileId { get; }

    public string? InvoiceTypeCode { get; }

    public string? Currency { get; }

    public IReadOnlyList<XElement> Lines { get; }

    public bool HasWithholding { get; }

    public decimal Tolerance => Options.RoundingTolerance;

    /// <summary>
    /// Tolerance for a value aggregated from <paramref name="parts"/> individually rounded amounts: each part may be
    /// off by half a kuruş, so the drift can grow with the number of parts (never below the base tolerance).
    /// </summary>
    public decimal AggregateTolerance(int parts) => Math.Max(Options.RoundingTolerance, 0.005m * parts);

    public void Report(RuleDescriptor rule, XObject? node, params object?[] args) => _findings.Add(new Finding(rule, node, args));

    public static string? Text(XElement? parent, XName name)
    {
        var value = parent?.Element(name)?.Value;
        return value is null ? null : value.Trim();
    }

    public static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>Parses an xsd:decimal lexical value (invariant culture, no thousands separators).</summary>
    public static decimal? Amount(string? value) =>
        decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite
                                | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static decimal? Amount(XElement? element) => element is null ? null : Amount(element.Value);

    /// <summary>
    /// The part of a TaxSubtotal that is added to the invoice amount. GIB marks computed-but-not-charged taxes (e.g. VAT
    /// on investment-incentive exemptions, see its YTB_Istisna samples) with a negative CalculationSequenceNumeric:
    /// such a subtotal still shows the computed amount but contributes nothing to the totals.
    /// </summary>
    public static decimal ChargedTax(XElement subtotal) =>
        Amount(subtotal.Element(Ns.Cbc + "CalculationSequenceNumeric")) is < 0m ? 0m : Amount(subtotal.Element(Ns.Cbc + "TaxAmount")) ?? 0m;

    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Invariant, trailing-zero-free rendering for messages ("20360" rather than "20360.00").</summary>
    public static string Show(decimal value) => value.ToString("0.##########", CultureInfo.InvariantCulture);
}
