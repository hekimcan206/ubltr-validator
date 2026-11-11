namespace UblTr.SampleGen;

/// <summary>A party: a company (VKN) or a person (TCKN). All values are synthetic.</summary>
public sealed record PartySpec(
    string Scheme,
    string TaxId,
    string? Name,
    string? FirstName,
    string? FamilyName,
    string City,
    string District,
    string Street,
    string TaxOffice);

public sealed record LineSpec(string Name, decimal Quantity, decimal Price, decimal VatPercent, decimal Allowance = 0m, string UnitCode = "C62");

/// <summary>Withholding (tevkifat) applied to the VAT, e.g. code 603 at 70 %.</summary>
public sealed record WithholdingSpec(string Code, decimal Percent);

/// <summary>VAT exemption (istisna) for zero-rated invoices, e.g. code 301.</summary>
public sealed record ExemptionSpec(string Code, string Reason);

/// <summary>
/// A synthetic invoice described at business level. <see cref="InvoiceWriter"/> turns it into UBL-TR XML and computes
/// every total, so a spec always produces an arithmetically consistent document.
/// </summary>
public sealed record InvoiceSpec
{
    public string Id { get; init; } = "SYN2026000000001";

    public Guid Uuid { get; init; } = Guid.Parse("4f3c2a1b-8d7e-4c6b-9a5f-0e1d2c3b4a59");

    public DateOnly IssueDate { get; init; } = new(2026, 11, 2);

    public string Profile { get; init; } = "TEMELFATURA";

    public string Type { get; init; } = "SATIS";

    public string Currency { get; init; } = "TRY";

    /// <summary>Document currency → TRY rate; required when <see cref="Currency"/> is not TRY.</summary>
    public decimal? ExchangeRate { get; init; }

    public required PartySpec Supplier { get; init; }

    public required PartySpec Customer { get; init; }

    public required IReadOnlyList<LineSpec> Lines { get; init; }

    /// <summary>Write a cac:TaxTotal on every line (UBL-TR allows omitting it).</summary>
    public bool LineTaxTotals { get; init; } = true;

    public WithholdingSpec? Withholding { get; init; }

    public ExemptionSpec? Exemption { get; init; }

    /// <summary>Number of the invoice being returned (IADE types).</summary>
    public string? ReturnedInvoiceId { get; init; }

    public string? Note { get; init; }
}
