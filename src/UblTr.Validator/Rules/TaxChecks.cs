using System.Xml.Linq;
using UblTr.Validator.CodeLists;
using static UblTr.Validator.Rules.InvoiceContext;

namespace UblTr.Validator.Rules;

internal static class TaxChecks
{
    private static readonly string[] ZeroVatExemptTypes = ["IADE", "YTBIADE", "IHRACKAYITLI", "OZELMATRAH", "SGK", "KONAKLAMAVERGISI"];
    private static readonly string[] WithholdingTypes = ["TEVKIFAT", "YTBTEVKIFAT", "IADE", "YTBIADE", "SGK", "SARJ", "SARJANLIK"];
    private static readonly string[] Tax4171Types = ["TEVKIFAT", "IADE", "SGK", "YTBIADE"];
    private static readonly string[] IstisnaTypes = ["ISTISNA", "IADE", "IHRACKAYITLI", "SGK", "YTBISTISNA", "YTBIADE"];
    private static readonly string[] SpecialBaseTypes = ["OZELMATRAH", "IADE", "SGK"];
    private static readonly string[] ExportRegisteredTypes = ["IHRACKAYITLI", "IADE", "SGK"];

    private const string Vat = "0015";

    public static void Run(InvoiceContext ctx)
    {
        var root = ctx.Root;
        var type = ctx.InvoiceTypeCode;
        var documentTaxTotals = root.Elements(N.TaxTotal).ToList();
        var withheld = root.Elements(N.WithholdingTaxTotal)
            .Select(w => Amount(w.Element(N.TaxAmount)) ?? 0m).Sum();

        foreach (var taxTotal in documentTaxTotals)
        {
            CheckTaxTotalSum(ctx, taxTotal, withheld);
            foreach (var subtotal in taxTotal.Elements(N.TaxSubtotal))
            {
                // Document subtotals aggregate one rounded tax per line, so they may drift by half a kuruş per line.
                CheckSubtotal(ctx, subtotal, ctx.AggregateTolerance(ctx.Lines.Count), documentLevel: true);
            }
        }

        foreach (var line in ctx.Lines)
        {
            foreach (var lineTaxTotal in line.Elements(N.TaxTotal))
            {
                CheckTaxTotalSum(ctx, lineTaxTotal,
                    line.Elements(N.WithholdingTaxTotal).Select(w => Amount(w.Element(N.TaxAmount)) ?? 0m).Sum());
            }

            foreach (var subtotal in line.Elements(N.TaxTotal).Elements(N.TaxSubtotal))
            {
                CheckSubtotal(ctx, subtotal, ctx.Tolerance, documentLevel: false);
            }

            foreach (var subtotal in line.Elements(N.WithholdingTaxTotal).Elements(N.TaxSubtotal))
            {
                CheckWithholdingSubtotal(ctx, subtotal);
            }
        }

        CheckBreakdown(ctx, documentTaxTotals);

        var withholdingTotals = root.Elements(N.WithholdingTaxTotal).ToList();
        if (withholdingTotals.Count > 0 && !WithholdingTypes.Contains(type))
        {
            ctx.Report(RuleCatalog.WithholdingType, withholdingTotals[0], type);
        }

        foreach (var subtotal in withholdingTotals.Elements(N.TaxSubtotal))
        {
            CheckWithholdingSubtotal(ctx, subtotal);
        }
    }

    private static void CheckTaxTotalSum(InvoiceContext ctx, XElement taxTotal, decimal withheld)
    {
        var declaredEl = taxTotal.Element(N.TaxAmount);
        if (Amount(declaredEl) is not { } declared)
        {
            return;
        }

        var sum = taxTotal.Elements(N.TaxSubtotal).Select(ChargedTax).Sum();
        var matchesGross = Math.Abs(declared - sum) <= ctx.Tolerance;
        var matchesNet = withheld != 0m && Math.Abs(declared - (sum - withheld)) <= ctx.Tolerance;
        if (!matchesGross && !matchesNet)
        {
            ctx.Report(RuleCatalog.TaxTotalSum, declaredEl, Show(declared), Show(sum), withheld != 0m
                ? new LocalizedText($" (tevkifat düşülmüş hali {Show(sum - withheld)})", $" (net of withholding {Show(sum - withheld)})")
                : LocalizedText.Same(""));
        }
    }

    private static void CheckSubtotal(InvoiceContext ctx, XElement subtotal, decimal tolerance, bool documentLevel)
    {
        var category = subtotal.Element(N.TaxCategory);
        var codeEl = category?.Element(N.TaxScheme)?.Element(N.TaxTypeCode);
        var code = codeEl?.Value.Trim();
        if (codeEl is not null && !ctx.CodeLists.Contains(CodeListSet.TaxTypes, code))
        {
            ctx.Report(RuleCatalog.TaxTypeCode, codeEl, code);
        }

        var taxAmountEl = subtotal.Element(N.TaxAmount);
        var taxAmount = Amount(taxAmountEl);
        var taxable = Amount(subtotal.Element(N.TaxableAmount));
        var percent = Amount(subtotal.Element(N.Percent));

        // Per-unit taxes (some SCT/ÖTV lists) are not a percentage of the base, so the rate check does not apply.
        if (taxAmount is { } t && taxable is { } b && percent is { } p && subtotal.Element(N.PerUnitAmount) is null)
        {
            var expected = Round2(b * p / 100m);
            if (Math.Abs(t - expected) > tolerance)
            {
                ctx.Report(RuleCatalog.TaxSubtotalRate, taxAmountEl, Show(b), Show(p), Show(t), Show(expected));
            }
        }

        if (!documentLevel || category is null)
        {
            return;
        }

        var type = ctx.InvoiceTypeCode;
        if (code == Vat && taxAmount == 0m && !ZeroVatExemptTypes.Contains(type)
            && IsBlank(Text(category, N.TaxExemptionReason)))
        {
            ctx.Report(RuleCatalog.ZeroVatReason, category, type);
        }

        if (code == "4171" && !Tax4171Types.Contains(type))
        {
            ctx.Report(RuleCatalog.Tax4171, codeEl, type);
        }

        CheckExemption(ctx, category, type);
    }

    private static void CheckExemption(InvoiceContext ctx, XElement category, string? type)
    {
        var codeEl = category.Element(N.TaxExemptionReasonCode);
        var code = codeEl?.Value.Trim();
        var lists = ctx.CodeLists;

        if (category.Element(N.TaxExemptionReason) is not null)
        {
            var investment = lists.Contains(CodeListSet.InvestmentIncentiveExemptionCodes, code)
                             && (ctx.ProfileId == "YATIRIMTESVIK" || lists.Contains(CodeListSet.InvestmentIncentiveEArchiveTypes, type));
            if (IsBlank(code) || !(lists.Contains(CodeListSet.TaxExemptionReasonCodes, code) || investment))
            {
                ctx.Report(RuleCatalog.ExemptionCode, (XObject?)codeEl ?? category, code);
            }
        }

        if (IsBlank(code))
        {
            return;
        }

        string[]? allowed = null;
        if (code != "555" && lists.Contains(CodeListSet.ExemptionCodesRequiringIstisna, code))
        {
            allowed = IstisnaTypes;
        }
        else if (lists.Contains(CodeListSet.SpecialBaseExemptionCodes, code))
        {
            allowed = SpecialBaseTypes;
        }
        else if (lists.Contains(CodeListSet.ExportRegisteredExemptionCodes, code))
        {
            allowed = ExportRegisteredTypes;
        }

        if (allowed is not null && !allowed.Contains(type))
        {
            ctx.Report(RuleCatalog.ExemptionType, codeEl, code, type, string.Join(", ", allowed));
        }
    }

    private static void CheckWithholdingSubtotal(InvoiceContext ctx, XElement subtotal)
    {
        var codeEl = subtotal.Element(N.TaxCategory)?.Element(N.TaxScheme)?.Element(N.TaxTypeCode);
        var code = codeEl?.Value.Trim() ?? "";
        // GIB concatenates the raw texts (code + percent), so "70.00" is not "70"; this mirrors that on purpose.
        var percent = Text(subtotal, N.Percent) ?? "";
        if (code.Length == 0 || percent.Length == 0
            || !ctx.CodeLists.Contains(CodeListSet.WithholdingTaxTypes, code)
            || !ctx.CodeLists.Contains(CodeListSet.WithholdingTaxTypesWithPercent, code + percent))
        {
            ctx.Report(RuleCatalog.WithholdingCode, (XObject?)codeEl ?? subtotal, code, percent);
        }
    }

    /// <summary>
    /// The document breakdown must equal the lines' breakdown per (tax code, rate). Only compared when every line
    /// carries its own TaxTotal: UBL-TR allows lines without one, and then there is nothing to reconcile.
    /// </summary>
    private static void CheckBreakdown(InvoiceContext ctx, List<XElement> documentTaxTotals)
    {
        if (ctx.Lines.Count == 0 || documentTaxTotals.Count == 0 || ctx.Lines.Any(l => l.Element(N.TaxTotal) is null))
        {
            return;
        }

        var lineTotals = new Dictionary<(string Code, decimal Percent), (decimal Tax, int Parts)>();
        foreach (var subtotal in ctx.Lines.SelectMany(l => l.Elements(N.TaxTotal).Elements(N.TaxSubtotal)))
        {
            var key = Key(subtotal);
            lineTotals.TryGetValue(key, out var acc);
            lineTotals[key] = (acc.Tax + ChargedTax(subtotal), acc.Parts + 1);
        }

        var documentTotals = new Dictionary<(string Code, decimal Percent), (decimal Tax, XElement Node)>();
        foreach (var subtotal in documentTaxTotals.Elements(N.TaxSubtotal))
        {
            var key = Key(subtotal);
            documentTotals.TryGetValue(key, out var acc);
            documentTotals[key] = (acc.Tax + ChargedTax(subtotal), acc.Node ?? subtotal);
        }

        foreach (var key in documentTotals.Keys.Union(lineTotals.Keys).OrderBy(k => k.Code, StringComparer.Ordinal).ThenBy(k => k.Percent))
        {
            var hasDoc = documentTotals.TryGetValue(key, out var doc);
            lineTotals.TryGetValue(key, out var lines);
            if (Math.Abs(doc.Tax - lines.Tax) > ctx.AggregateTolerance(Math.Max(1, lines.Parts)))
            {
                ctx.Report(RuleCatalog.TaxBreakdown, hasDoc ? doc.Node : documentTaxTotals[0], key.Code, Show(key.Percent),
                    Show(doc.Tax), Show(lines.Tax));
            }
        }
    }

    private static (string Code, decimal Percent) Key(XElement subtotal) => (
        subtotal.Element(N.TaxCategory)?.Element(N.TaxScheme)?.Element(N.TaxTypeCode)?.Value.Trim() ?? "",
        Amount(subtotal.Element(N.Percent)) ?? 0m);
}
