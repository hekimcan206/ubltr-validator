namespace UblTr.Validator;

public sealed record ValidationOptions
{
    /// <summary>e-Fatura, e-Arşiv, or Auto (decided from cbc:ProfileID).</summary>
    public DocumentKind Kind { get; init; } = DocumentKind.Auto;

    /// <summary>"Today" for the issue-date check. Defaults to the current date in Türkiye (UTC+3, no DST since 2016).</summary>
    public DateOnly Today { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    /// <summary>Allowed difference for one rounded amount (one kuruş).</summary>
    public decimal RoundingTolerance { get; init; } = 0.01m;

    /// <summary>Rule ids to suppress (e.g. "PTY-003" while test data uses fake VKNs).</summary>
    public IReadOnlySet<string> DisabledRules { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Findings kept per rule. A systematic error (wrong currencyID on 50,000 lines) should produce a readable report
    /// rather than 50,000 identical entries; the report says how many were dropped.
    /// </summary>
    public int MaxFindingsPerRule { get; init; } = 100;

    /// <summary>Skip XSD validation (business rules only). Used when the GIB schemas are unavailable.</summary>
    public bool SkipSchema { get; init; }
}
