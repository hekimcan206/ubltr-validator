using System.Xml.Linq;

namespace UblTr.Validator.Rules;

/// <summary>
/// Pre-built XNames. Converting a string to an XName (Ns.Cbc + "TaxAmount") looks the name up in a shared, thread-safe
/// table on every call; with dozens of lookups per invoice line that dominated the rule time on large invoices
/// (docs/benchmarks.md, "XName cache"). Static instances make each lookup a reference comparison.
/// </summary>
internal static class N
{
    public static readonly XName AllowanceTotalAmount = Ns.Cbc + "AllowanceTotalAmount";
    public static readonly XName Amount = Ns.Cbc + "Amount";
    public static readonly XName BaseQuantity = Ns.Cbc + "BaseQuantity";
    public static readonly XName CalculationRate = Ns.Cbc + "CalculationRate";
    public static readonly XName CalculationSequenceNumeric = Ns.Cbc + "CalculationSequenceNumeric";
    public static readonly XName ChargeIndicator = Ns.Cbc + "ChargeIndicator";
    public static readonly XName ChargeTotalAmount = Ns.Cbc + "ChargeTotalAmount";
    public static readonly XName CopyIndicator = Ns.Cbc + "CopyIndicator";
    public static readonly XName CustomizationID = Ns.Cbc + "CustomizationID";
    public static readonly XName DocumentCurrencyCode = Ns.Cbc + "DocumentCurrencyCode";
    public static readonly XName DocumentTypeCode = Ns.Cbc + "DocumentTypeCode";
    public static readonly XName FamilyName = Ns.Cbc + "FamilyName";
    public static readonly XName FirstName = Ns.Cbc + "FirstName";
    public static readonly XName ID = Ns.Cbc + "ID";
    public static readonly XName InvoiceTypeCode = Ns.Cbc + "InvoiceTypeCode";
    public static readonly XName InvoicedQuantity = Ns.Cbc + "InvoicedQuantity";
    public static readonly XName IssueDate = Ns.Cbc + "IssueDate";
    public static readonly XName LineCountNumeric = Ns.Cbc + "LineCountNumeric";
    public static readonly XName LineExtensionAmount = Ns.Cbc + "LineExtensionAmount";
    public static readonly XName Name = Ns.Cbc + "Name";
    public static readonly XName PayableAmount = Ns.Cbc + "PayableAmount";
    public static readonly XName PayableRoundingAmount = Ns.Cbc + "PayableRoundingAmount";
    public static readonly XName PerUnitAmount = Ns.Cbc + "PerUnitAmount";
    public static readonly XName Percent = Ns.Cbc + "Percent";
    public static readonly XName PrepaidAmount = Ns.Cbc + "PrepaidAmount";
    public static readonly XName PriceAmount = Ns.Cbc + "PriceAmount";
    public static readonly XName ProfileID = Ns.Cbc + "ProfileID";
    public static readonly XName SourceCurrencyCode = Ns.Cbc + "SourceCurrencyCode";
    public static readonly XName TargetCurrencyCode = Ns.Cbc + "TargetCurrencyCode";
    public static readonly XName TaxAmount = Ns.Cbc + "TaxAmount";
    public static readonly XName TaxExclusiveAmount = Ns.Cbc + "TaxExclusiveAmount";
    public static readonly XName TaxExemptionReason = Ns.Cbc + "TaxExemptionReason";
    public static readonly XName TaxExemptionReasonCode = Ns.Cbc + "TaxExemptionReasonCode";
    public static readonly XName TaxInclusiveAmount = Ns.Cbc + "TaxInclusiveAmount";
    public static readonly XName TaxTypeCode = Ns.Cbc + "TaxTypeCode";
    public static readonly XName TaxableAmount = Ns.Cbc + "TaxableAmount";
    public static readonly XName UBLVersionID = Ns.Cbc + "UBLVersionID";
    public static readonly XName UUID = Ns.Cbc + "UUID";
    public static readonly XName AccountingCustomerParty = Ns.Cac + "AccountingCustomerParty";
    public static readonly XName AccountingSupplierParty = Ns.Cac + "AccountingSupplierParty";
    public static readonly XName AllowanceCharge = Ns.Cac + "AllowanceCharge";
    public static readonly XName BillingReference = Ns.Cac + "BillingReference";
    public static readonly XName InvoiceDocumentReference = Ns.Cac + "InvoiceDocumentReference";
    public static readonly XName InvoiceLine = Ns.Cac + "InvoiceLine";
    public static readonly XName LegalMonetaryTotal = Ns.Cac + "LegalMonetaryTotal";
    public static readonly XName Party = Ns.Cac + "Party";
    public static readonly XName PartyIdentification = Ns.Cac + "PartyIdentification";
    public static readonly XName PartyName = Ns.Cac + "PartyName";
    public static readonly XName Person = Ns.Cac + "Person";
    public static readonly XName Price = Ns.Cac + "Price";
    public static readonly XName PricingExchangeRate = Ns.Cac + "PricingExchangeRate";
    public static readonly XName TaxCategory = Ns.Cac + "TaxCategory";
    public static readonly XName TaxScheme = Ns.Cac + "TaxScheme";
    public static readonly XName TaxSubtotal = Ns.Cac + "TaxSubtotal";
    public static readonly XName TaxTotal = Ns.Cac + "TaxTotal";
    public static readonly XName WithholdingTaxTotal = Ns.Cac + "WithholdingTaxTotal";

    public static readonly XName CurrencyId = "currencyID";
    public static readonly XName SchemeId = "schemeID";
}
