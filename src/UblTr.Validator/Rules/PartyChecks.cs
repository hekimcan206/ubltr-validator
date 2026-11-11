using System.Globalization;
using System.Xml.Linq;
using UblTr.Validator.CodeLists;
using UblTr.Validator.Identifiers;
using static UblTr.Validator.Rules.InvoiceContext;

namespace UblTr.Validator.Rules;

internal static class PartyChecks
{
    private static readonly LocalizedText Supplier = new("Satıcı (AccountingSupplierParty)", "Supplier (AccountingSupplierParty)");
    private static readonly LocalizedText Customer = new("Alıcı (AccountingCustomerParty)", "Customer (AccountingCustomerParty)");

    public static void Run(InvoiceContext ctx)
    {
        CheckParty(ctx, ctx.Root.Element(N.AccountingSupplierParty), Supplier);
        CheckParty(ctx, ctx.Root.Element(N.AccountingCustomerParty), Customer);
    }

    private static void CheckParty(InvoiceContext ctx, XElement? wrapper, LocalizedText role)
    {
        var party = wrapper?.Element(N.Party);
        if (party is null)
        {
            ctx.Report(RuleCatalog.PartyTaxId, (XObject?)wrapper ?? ctx.Root, role,
                new LocalizedText("cac:Party elemanı yok", "cac:Party is missing"));
            return;
        }

        int vknCount = 0, tcknCount = 0;
        foreach (var id in party.Elements(N.PartyIdentification).Elements(N.ID))
        {
            var scheme = (string?)id.Attribute(N.SchemeId);
            var value = id.Value.Trim();
            if (!ctx.CodeLists.Contains(CodeListSet.PartyIdentificationSchemes, scheme))
            {
                ctx.Report(RuleCatalog.PartySchemeId, id, role, scheme);
            }

            if (scheme == "VKN")
            {
                vknCount++;
                if (value.Length != 10 || !TaxIdentifiers.IsDigits(value))
                {
                    ctx.Report(RuleCatalog.VknLength, id, role, value);
                }
                else if (!TaxIdentifiers.IsValidVkn(value))
                {
                    ctx.Report(RuleCatalog.VknChecksum, id, role, value,
                        TaxIdentifiers.VknCheckDigit(value.AsSpan(0, 9)).ToString(CultureInfo.InvariantCulture));
                }
            }
            else if (scheme == "TCKN")
            {
                tcknCount++;
                if (value.Length != 11 || !TaxIdentifiers.IsDigits(value) || value[0] == '0')
                {
                    ctx.Report(RuleCatalog.TcknLength, id, role, value);
                }
                else if (!TaxIdentifiers.IsValidTckn(value))
                {
                    var (tenth, eleventh) = TaxIdentifiers.TcknCheckDigits(value.AsSpan(0, 9));
                    ctx.Report(RuleCatalog.TcknChecksum, id, role, value, $"{tenth}{eleventh}");
                }
            }
        }

        if (vknCount == 1 && tcknCount == 1)
        {
            ctx.Report(RuleCatalog.PartyTaxId, party, role,
                new LocalizedText("hem VKN hem TCKN yazılmış", "both a VKN and a TCKN are present"));
        }
        else if (vknCount != 1 && tcknCount != 1)
        {
            ctx.Report(RuleCatalog.PartyTaxId, party, role, vknCount + tcknCount == 0
                ? new LocalizedText("VKN veya TCKN yok", "no VKN or TCKN")
                : new LocalizedText($"{vknCount} VKN ve {tcknCount} TCKN var, tam olarak bir tane olmalı",
                    $"{vknCount} VKN and {tcknCount} TCKN found, exactly one is required"));
        }

        if (vknCount > 0 && IsBlank(Text(party.Element(N.PartyName), N.Name)))
        {
            ctx.Report(RuleCatalog.PartyName, party, role);
        }

        if (tcknCount > 0)
        {
            var person = party.Element(N.Person);
            if (IsBlank(Text(person, N.FirstName)) || IsBlank(Text(person, N.FamilyName)))
            {
                ctx.Report(RuleCatalog.PartyPerson, (XObject?)person ?? party, role);
            }
        }
    }
}
