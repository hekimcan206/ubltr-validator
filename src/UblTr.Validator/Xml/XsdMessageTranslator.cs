using System.Text.RegularExpressions;

namespace UblTr.Validator.Xml;

/// <summary>
/// Turns System.Xml schema-validation messages (English, full of namespace URNs) into short Turkish and English
/// explanations with a concrete fix. Unknown message shapes fall back to the original text, so nothing is lost.
/// </summary>
internal static partial class XsdMessageTranslator
{
    private static readonly (string Uri, string Prefix)[] Prefixes =
    [
        ("urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2", "cbc"),
        ("urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2", "cac"),
        ("urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2", "ext"),
        ("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", ""),
        ("urn:oasis:names:specification:ubl:schema:xsd:UnqualifiedDataTypes-2", "udt"),
        ("urn:un:unece:uncefact:data:specification:CoreComponentTypeSchemaModule:2", "cct"),
        ("http://www.w3.org/2000/09/xmldsig#", "ds"),
    ];

    [GeneratedRegex(@"^The element '(?<parent>[^']+)'(?: in namespace '(?<pns>[^']+)')? has invalid child element '(?<child>[^']+)'(?: in namespace '(?<cns>[^']+)')?\.(?: List of possible elements expected: '(?<exp>[^']+)'(?: in namespace '(?<ens>[^']+)')?(?<more>.*))?", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidChild();

    [GeneratedRegex(@"^The element '(?<parent>[^']+)'(?: in namespace '(?<pns>[^']+)')? has incomplete content\.(?: List of possible elements expected: '(?<exp>[^']+)'(?: in namespace '(?<ens>[^']+)')?(?<more>.*))?", RegexOptions.CultureInvariant)]
    private static partial Regex IncompleteContent();

    [GeneratedRegex(@"^The '(?<name>[^']+)' (?<kind>element|attribute) is invalid - The value '(?<value>[^']*)' is invalid according to its datatype '(?<type>[^']+)'(?: - (?<detail>.*))?", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex InvalidValue();

    [GeneratedRegex(@"^The required attribute '(?<attr>[^']+)' is missing\.", RegexOptions.CultureInvariant)]
    private static partial Regex MissingAttribute();

    [GeneratedRegex(@"^The '(?<attr>[^']+)' attribute is not declared\.", RegexOptions.CultureInvariant)]
    private static partial Regex UndeclaredAttribute();

    [GeneratedRegex(@"^The element '(?<el>[^']+)'(?: in namespace '(?<ns>[^']+)')? cannot contain text", RegexOptions.CultureInvariant)]
    private static partial Regex NoText();

    public static (LocalizedText Message, LocalizedText Fix) Translate(string message)
    {
        Match m;
        if ((m = InvalidChild().Match(message)).Success)
        {
            var parent = Q(m, "parent", "pns");
            var child = Q(m, "child", "cns");
            if (m.Groups["exp"].Success)
            {
                var expected = Expected(m);
                return (
                    new($"'{parent}' içinde beklenmeyen eleman '{child}'. Bu konumda beklenen: {expected}.",
                        $"Unexpected element '{child}' inside '{parent}'. Expected here: {expected}."),
                    new($"Elemanları UBL-TR şemasındaki sıraya göre dizin; '{child}' yanlış yerde, eksik bir zorunlu elemandan sonra veya yanlış isim alanında olabilir. Beklenen elemanlardan biri ({expected}) eksik olabilir.",
                        $"Put the elements in the order the UBL-TR schema defines; '{child}' may be misplaced, follow a missing mandatory element, or use the wrong namespace. One of the expected elements ({expected}) may be missing."));
            }

            return (
                new($"'{parent}' içinde '{child}' elemanına izin verilmiyor (fazla veya tekrarlanmış eleman).",
                    $"'{child}' is not allowed inside '{parent}' (extra or repeated element)."),
                new($"'{child}' elemanını kaldırın ya da doğru üst elemanın altına taşıyın.",
                    $"Remove '{child}' or move it under the correct parent element."));
        }

        if ((m = IncompleteContent().Match(message)).Success)
        {
            var parent = Q(m, "parent", "pns");
            if (!m.Groups["exp"].Success)
            {
                return (
                    new($"'{parent}' eksik: zorunlu bir alt eleman yok.", $"'{parent}' is incomplete: a mandatory child element is missing."),
                    new($"'{parent}' içine şemanın istediği alt elemanı ekleyin (UBLExtensions için imza veya başka bir uzantı elemanı).",
                        $"Add the child element the schema requires inside '{parent}' (for UBLExtensions: the signature or another extension element)."));
            }

            var expected = Expected(m);
            return (
                new($"'{parent}' eksik: zorunlu eleman(lar) yok. Beklenen: {expected}.",
                    $"'{parent}' is incomplete: mandatory element(s) missing. Expected: {expected}."),
                new($"'{parent}' sonuna {expected} elemanını ekleyin.", $"Add {expected} at the end of '{parent}'."));
        }

        if ((m = InvalidValue().Match(message)).Success)
        {
            var name = Shorten(m.Groups["name"].Value);
            var value = m.Groups["value"].Value;
            var type = Shorten(m.Groups["type"].Value);
            var isAttribute = m.Groups["kind"].Value == "attribute";
            var hint = TypeHint(type);
            return (
                new($"{(isAttribute ? "Nitelik" : "Eleman")} '{name}' değeri '{value}' '{type}' veri tipine uymuyor.",
                    $"{(isAttribute ? "Attribute" : "Element")} '{name}' value '{value}' does not match datatype '{type}'."),
                new($"Değeri '{type}' biçiminde yazın. {hint.Tr}", $"Write the value in '{type}' format. {hint.En}"));
        }

        if ((m = MissingAttribute().Match(message)).Success)
        {
            var attr = Shorten(m.Groups["attr"].Value);
            return (
                new($"Zorunlu nitelik '{attr}' eksik.", $"Required attribute '{attr}' is missing."),
                new(attr == "currencyID" ? "Tutar elemanına para birimini ekleyin: currencyID=\"TRY\"."
                        : attr == "unitCode" ? "Miktar elemanına birim kodunu ekleyin: unitCode=\"C62\" (adet)."
                        : $"Elemana '{attr}' niteliğini ekleyin.",
                    attr == "currencyID" ? "Add the currency to the amount element: currencyID=\"TRY\"."
                        : attr == "unitCode" ? "Add the unit code to the quantity element: unitCode=\"C62\" (piece)."
                        : $"Add the '{attr}' attribute to the element."));
        }

        if ((m = UndeclaredAttribute().Match(message)).Success)
        {
            var attr = Shorten(m.Groups["attr"].Value);
            return (
                new($"Nitelik '{attr}' şemada tanımlı değil.", $"Attribute '{attr}' is not declared in the schema."),
                new($"'{attr}' niteliğini kaldırın ya da yazımını düzeltin.", $"Remove the '{attr}' attribute or fix its spelling."));
        }

        if ((m = NoText().Match(message)).Success)
        {
            var el = Q(m, "el", "ns");
            return (
                new($"'{el}' elemanı metin içeremez; yalnızca alt elemanlar içerir.", $"'{el}' cannot contain text; it only holds child elements."),
                new($"'{el}' içindeki metni uygun alt elemana taşıyın.", $"Move the text inside '{el}' into the appropriate child element."));
        }

        var shortened = Shorten(message);
        return (
            new($"Şema hatası: {shortened}", $"Schema error: {shortened}"),
            new("Elemanı UBL-TR 1.2.1 şemasıyla (GİB xsdrt) karşılaştırarak düzeltin.", "Compare the element with the UBL-TR 1.2.1 schema (GIB xsdrt) and correct it."));
    }

    private static string Expected(Match m)
    {
        var prefix = PrefixOf(m.Groups["ens"].Success ? m.Groups["ens"].Value : "");
        var names = m.Groups["exp"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(n => prefix.Length == 0 || n.Contains(':', StringComparison.Ordinal) ? Shorten(n) : $"{prefix}:{n}");
        var list = string.Join(", ", names);
        var more = m.Groups["more"].Value;
        return more.Contains("as well as", StringComparison.Ordinal) ? list + ", ..." : list;
    }

    private static string Q(Match m, string name, string ns)
    {
        var local = m.Groups[name].Value;
        var prefix = m.Groups[ns].Success ? PrefixOf(m.Groups[ns].Value) : "";
        return prefix.Length == 0 ? local : $"{prefix}:{local}";
    }

    private static string PrefixOf(string uri) =>
        Prefixes.FirstOrDefault(p => p.Uri == uri).Prefix ?? "";

    private static string Shorten(string text)
    {
        foreach (var (uri, prefix) in Prefixes)
        {
            text = text.Replace(uri + ":", prefix.Length == 0 ? "" : prefix + ":", StringComparison.Ordinal);
        }

        return text;
    }

    private static (string Tr, string En) TypeHint(string type) => type switch
    {
        _ when type.Contains("decimal", StringComparison.OrdinalIgnoreCase) || type.Contains("Amount", StringComparison.Ordinal)
            || type.Contains("Quantity", StringComparison.Ordinal) || type.Contains("Numeric", StringComparison.Ordinal)
            || type.Contains("Rate", StringComparison.Ordinal) || type.Contains("Percent", StringComparison.Ordinal) =>
            ("Ondalık ayıracı nokta olmalı, binlik ayıracı kullanılmamalı (ör. 1234.50).", "Use a dot as decimal separator and no thousands separator (e.g. 1234.50)."),
        _ when type.Contains("Date", StringComparison.Ordinal) =>
            ("Tarih YYYY-AA-GG biçiminde olmalı (ör. 2026-11-11).", "Dates use YYYY-MM-DD (e.g. 2026-11-11)."),
        _ when type.Contains("Time", StringComparison.Ordinal) =>
            ("Saat SS:DD:ss biçiminde olmalı (ör. 14:30:00).", "Times use HH:MM:SS (e.g. 14:30:00)."),
        _ when type.Contains("Indicator", StringComparison.Ordinal) || type.Contains("boolean", StringComparison.OrdinalIgnoreCase) =>
            ("Değer 'true' veya 'false' olmalı.", "The value must be 'true' or 'false'."),
        _ => ("", ""),
    };
}
