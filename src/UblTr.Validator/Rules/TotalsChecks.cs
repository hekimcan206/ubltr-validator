using System.Text.RegularExpressions;
using System.Xml.Linq;
using static UblTr.Validator.Rules.InvoiceContext;

namespace UblTr.Validator.Rules;

/// <summary>
/// Arithmetic consistency of lines and LegalMonetaryTotal. GIB's Schematron never adds numbers up, yet a total that
/// does not match its parts is the most common reason a buyer rejects an invoice, so these checks are the core
/// addition of this project. Values that are missing or not decimals are skipped here: XSD-001 already reports them.
/// </summary>
internal static partial class TotalsChecks
{
    private static readonly string[] MonetaryTotalFields =
        ["LineExtensionAmount", "TaxExclusiveAmount", "TaxInclusiveAmount", "AllowanceTotalAmount", "PayableAmount"];

    // GIB decimalCheck: at most 2 decimals (kuruş).
    [GeneratedRegex(@"^\s*[0-9][0-9]{0,16}(,[0-9]{3})*(\.[0-9]{1,2}\s*)?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    public static void Run(InvoiceContext ctx)
    {
        var root = ctx.Root;
        var lmt = root.Element(Ns.Cac + "LegalMonetaryTotal");

        CheckFormats(ctx, lmt);

        var lineSum = 0m;
        var linesComplete = true;
        foreach (var line in ctx.Lines)
        {
            var extEl = line.Element(Ns.Cbc + "LineExtensionAmount");
            var ext = Amount(extEl);
            if (ext is null)
            {
                linesComplete = false;
                continue;
            }

            lineSum += ext.Value;
            CheckLine(ctx, line, extEl!, ext.Value);
        }

        if (lmt is null)
        {
            return;
        }

        var lineExtEl = lmt.Element(Ns.Cbc + "LineExtensionAmount");
        var lineExt = Amount(lineExtEl);
        if (linesComplete && lineExt is { } declaredLines && Math.Abs(declaredLines - lineSum) > ctx.Tolerance)
        {
            ctx.Report(RuleCatalog.LineTotal, lineExtEl, Show(declaredLines), Show(lineSum));
        }

        var allowance = Amount(lmt.Element(Ns.Cbc + "AllowanceTotalAmount")) ?? 0m;
        var charge = Amount(lmt.Element(Ns.Cbc + "ChargeTotalAmount")) ?? 0m;
        var exclusiveEl = lmt.Element(Ns.Cbc + "TaxExclusiveAmount");
        var exclusive = Amount(exclusiveEl);
        if (lineExt is { } le && exclusive is { } ex && Math.Abs(ex - (le - allowance + charge)) > ctx.Tolerance)
        {
            ctx.Report(RuleCatalog.TaxExclusive, exclusiveEl, Show(ex), Show(le - allowance + charge));
        }

        // Taxes added to the price: every document-level TaxSubtotal. The withheld part is deducted only from the payable amount.
        var taxes = root.Elements(Ns.Cac + "TaxTotal").Elements(Ns.Cac + "TaxSubtotal")
            .Select(ChargedTax).Sum();
        var inclusiveEl = lmt.Element(Ns.Cbc + "TaxInclusiveAmount");
        var inclusive = Amount(inclusiveEl);
        if (exclusive is { } exc && inclusive is { } inc && Math.Abs(inc - (exc + taxes)) > ctx.Tolerance)
        {
            ctx.Report(RuleCatalog.TaxInclusive, inclusiveEl, Show(inc), Show(exc + taxes));
        }

        var prepaid = Amount(lmt.Element(Ns.Cbc + "PrepaidAmount")) ?? 0m;
        var rounding = Amount(lmt.Element(Ns.Cbc + "PayableRoundingAmount")) ?? 0m;
        var withheld = root.Elements(Ns.Cac + "WithholdingTaxTotal")
            .Select(w => Amount(w.Element(Ns.Cbc + "TaxAmount")) ?? 0m).Sum();
        var payableEl = lmt.Element(Ns.Cbc + "PayableAmount");
        if (inclusive is { } incl && Amount(payableEl) is { } payable)
        {
            var expected = incl - prepaid + rounding - withheld;
            if (Math.Abs(payable - expected) > ctx.Tolerance)
            {
                ctx.Report(RuleCatalog.Payable, payableEl, Show(payable), Show(expected));
            }
        }
    }

    private static void CheckLine(InvoiceContext ctx, XElement line, XElement extEl, decimal declared)
    {
        var quantity = Amount(line.Element(Ns.Cbc + "InvoicedQuantity"));
        var price = line.Element(Ns.Cac + "Price");
        var priceAmount = Amount(price?.Element(Ns.Cbc + "PriceAmount"));
        if (quantity is null || priceAmount is null)
        {
            return;
        }

        // UBL: PriceAmount is the price of BaseQuantity units (1 when absent).
        var baseQuantity = Amount(price!.Element(Ns.Cbc + "BaseQuantity"));
        var gross = quantity.Value * priceAmount.Value / (baseQuantity is > 0m ? baseQuantity.Value : 1m);

        var adjustments = 0m;
        foreach (var ac in line.Elements(Ns.Cac + "AllowanceCharge"))
        {
            var amount = Amount(ac.Element(Ns.Cbc + "Amount")) ?? 0m;
            var isCharge = Text(ac, Ns.Cbc + "ChargeIndicator") is "true" or "1";
            adjustments += isCharge ? amount : -amount;
        }

        var expected = Round2(gross + adjustments);
        if (Math.Abs(declared - expected) > ctx.Tolerance)
        {
            ctx.Report(RuleCatalog.LineAmount, extEl, Text(line, Ns.Cbc + "ID"), Show(declared), Show(expected));
        }
    }

    private static void CheckFormats(InvoiceContext ctx, XElement? lmt)
    {
        if (lmt is not null)
        {
            foreach (var name in MonetaryTotalFields)
            {
                var el = lmt.Element(Ns.Cbc + name);
                if (el is not null && !AmountPattern().IsMatch(el.Value))
                {
                    ctx.Report(RuleCatalog.AmountFormat, el, "cac:LegalMonetaryTotal/cbc:" + name, el.Value.Trim());
                }
            }
        }

        foreach (var taxAmount in ctx.Root.Elements(Ns.Cac + "TaxTotal").Elements(Ns.Cbc + "TaxAmount"))
        {
            if (!AmountPattern().IsMatch(taxAmount.Value))
            {
                ctx.Report(RuleCatalog.AmountFormat, taxAmount, "cac:TaxTotal/cbc:TaxAmount", taxAmount.Value.Trim());
            }
        }
    }
}
