using System.Collections.Frozen;
using System.Xml;
using System.Xml.Linq;

namespace UblTr.Validator.CodeLists;

/// <summary>
/// Named code lists (ProfileIDType, InvoiceTypeCodeList, TaxType, ...) keyed by the names GIB uses in its Schematron,
/// so a rule can be traced back to the exact list it checks.
/// </summary>
public sealed class CodeListSet
{
    public const string ProfileIds = "ProfileIDType";
    public const string ProfileIdsEArchive = "ProfileIDTypeEarchive";
    public const string InvoiceTypeCodes = "InvoiceTypeCodeList";
    public const string PartyIdentificationSchemes = "PartyIdentificationIDType";
    public const string TaxTypes = "TaxType";
    public const string WithholdingTaxTypes = "WithholdingTaxType";
    public const string WithholdingTaxTypesWithPercent = "WithholdingTaxTypeWithPercent";
    public const string TaxExemptionReasonCodes = "TaxExemptionReasonCodeType";
    public const string InvestmentIncentiveExemptionCodes = "YatirimTesvikTaxExemptionReasonCodeType";
    public const string ExemptionCodesRequiringIstisna = "istisnaTaxExemptionReasonCodeType";
    public const string SpecialBaseExemptionCodes = "ozelMatrahTaxExemptionReasonCodeType";
    public const string ExportRegisteredExemptionCodes = "ihracExemptionReasonCodeType";
    public const string InvestmentIncentiveEArchiveTypes = "YatirimTesvikEArsivInvoiceTypeCodeList";
    public const string Currencies = "CurrencyCodeList";
    public const string UnitCodes = "UnitCodeList";

    private readonly FrozenDictionary<string, FrozenSet<string>> _lists;

    private CodeListSet(IDictionary<string, FrozenSet<string>> lists, string source)
    {
        _lists = lists.ToFrozenDictionary(StringComparer.Ordinal);
        Source = source;
    }

    /// <summary>Where the lists came from (the fetched GIB file or the built-in snapshot).</summary>
    public string Source { get; }

    public IEnumerable<string> Names => _lists.Keys;

    public static CodeListSet BuiltIn { get; } = new(
        CodeListSnapshot.Lists.ToDictionary(kv => kv.Key, kv => Split(kv.Value)),
        CodeListSnapshot.Version);

    /// <summary>True when the list is known; unknown lists make the dependent rule skip instead of guessing.</summary>
    public bool Has(string list) => _lists.ContainsKey(list);

    public bool Contains(string list, string? value) =>
        value is not null && _lists.TryGetValue(list, out var set) && set.Contains(value);

    public IReadOnlyCollection<string> Get(string list) =>
        _lists.TryGetValue(list, out var set) ? set : FrozenSet<string>.Empty;

    /// <summary>
    /// Reads GIB's UBL-TR_Codelist.xml. GIB encodes every list as a Schematron variable whose value is an XPath
    /// string literal of the form <c>',A,B,C,'</c> (so the Schematron can test <c>contains($list, concat(',',x,','))</c>).
    /// </summary>
    public static CodeListSet LoadGibCodelist(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, settings);
        var doc = XDocument.Load(reader);
        XNamespace sch = "http://purl.oclc.org/dsdl/schematron";
        var lists = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal);
        foreach (var let in doc.Descendants(sch + "let"))
        {
            var name = (string?)let.Attribute("name");
            var value = ((string?)let.Attribute("value"))?.Trim();
            if (name is null || value is null || value.Length < 4 || !value.StartsWith("',", StringComparison.Ordinal)
                || !value.EndsWith(",'", StringComparison.Ordinal))
            {
                continue;
            }

            lists[name] = Split(value[2..^2]);
        }

        // Lists the GIB file does not define (none today) fall back to the snapshot rather than disabling a rule.
        foreach (var (name, value) in CodeListSnapshot.Lists)
        {
            lists.TryAdd(name, Split(value));
        }

        return new CodeListSet(lists, $"GIB {Path.GetFileName(path)} (fetched package)");
    }

    private static FrozenSet<string> Split(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToFrozenSet(StringComparer.Ordinal);
}
