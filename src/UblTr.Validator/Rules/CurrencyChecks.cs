using System.Text.RegularExpressions;
using UblTr.Validator.CodeLists;
using static UblTr.Validator.Rules.InvoiceContext;

namespace UblTr.Validator.Rules;

internal static partial class CurrencyChecks
{
    private static readonly string[] CurrencyElements =
        ["DocumentCurrencyCode", "TaxCurrencyCode", "PricingCurrencyCode", "PaymentCurrencyCode", "PaymentAlternativeCurrencyCode"];

    // GIB CurrencyCodeCheck: up to 6 decimals for the exchange rate.
    [GeneratedRegex(@"^\s*[0-9][0-9]{0,16}(,[0-9]{3})*(\.[0-9]{1,6}\s*)?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RatePattern();

    public static void Run(InvoiceContext ctx)
    {
        var root = ctx.Root;
        foreach (var name in CurrencyElements)
        {
            var el = root.Element(Ns.Cbc + name);
            if (el is null)
            {
                continue;
            }

            var code = el.Value.Trim();
            if (!ctx.CodeLists.Contains(CodeListSet.Currencies, code))
            {
                ctx.Report(RuleCatalog.CurrencyCode, el, "cbc:" + name, code);
            }
        }

        var currency = ctx.Currency;
        if (currency is null)
        {
            return;
        }

        var exchange = root.Element(Ns.Cac + "PricingExchangeRate");
        var rateEl = exchange?.Element(Ns.Cbc + "CalculationRate");
        if (currency != "TRY")
        {
            if (rateEl is null)
            {
                ctx.Report(RuleCatalog.ExchangeRateRequired, root.Element(Ns.Cbc + "DocumentCurrencyCode"), currency);
            }
            else if (!RatePattern().IsMatch(rateEl.Value) || Amount(rateEl) is not > 0m)
            {
                ctx.Report(RuleCatalog.ExchangeRateFormat, rateEl, rateEl.Value.Trim());
            }

            if (exchange is not null)
            {
                var source = Text(exchange, Ns.Cbc + "SourceCurrencyCode");
                var target = Text(exchange, Ns.Cbc + "TargetCurrencyCode");
                if (source != currency || target != "TRY")
                {
                    ctx.Report(RuleCatalog.ExchangeRateCurrencies, exchange, source, target, currency);
                }
            }
        }

        // One pass over every amount. currencyID only appears on UBL AmountType elements.
        foreach (var amount in root.Descendants())
        {
            var attr = amount.Attribute("currencyID");
            if (attr is not null && attr.Value != currency)
            {
                ctx.Report(RuleCatalog.AmountCurrency, attr, Ns.Qualified(amount.Name), attr.Value, currency);
            }
        }
    }
}
