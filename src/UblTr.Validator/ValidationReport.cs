using UblTr.Validator.Rules;

namespace UblTr.Validator;

/// <summary>Which e-document regime the invoice is validated against.</summary>
public enum DocumentKind
{
    /// <summary>Decide from cbc:ProfileID: EARSIVFATURA means e-Arsiv, anything else e-Fatura.</summary>
    Auto,
    EFatura,
    EArsiv,
}

/// <summary>Key header values, echoed in reports so a failing file can be identified without opening it.</summary>
public sealed record DocumentSummary(
    string? Id,
    string? Uuid,
    string? ProfileId,
    string? InvoiceTypeCode,
    string? Currency,
    DocumentKind Kind,
    int LineCount);

/// <summary>The language-neutral result of validating one document.</summary>
public sealed record ValidationResult(
    DocumentSummary? Document,
    IReadOnlyList<Finding> Findings,
    bool SchemaValidated,
    string CodeListSource,
    long SizeBytes,
    int DroppedFindings = 0)
{
    public int ErrorCount => Findings.Count(f => f.Rule.Severity == Severity.Error);

    public int WarningCount => Findings.Count(f => f.Rule.Severity == Severity.Warning);

    public bool IsValid => ErrorCount == 0;

    public ValidationReport ToReport(Language language, string? source = null) => new(
        source,
        IsValid,
        ErrorCount,
        WarningCount,
        language == Language.En ? "en" : "tr",
        SchemaValidated,
        CodeListSource,
        DroppedFindings,
        Document,
        [.. Findings
            .OrderBy(f => f.Line ?? int.MaxValue)
            .ThenBy(f => f.Column ?? int.MaxValue)
            .ThenBy(f => f.Rule.Id, StringComparer.Ordinal)
            .Select(f => f.Localize(language))]);
}

/// <summary>The rendered report (CLI JSON output and HTTP API response body).</summary>
public sealed record ValidationReport(
    string? Source,
    bool Valid,
    int Errors,
    int Warnings,
    string Language,
    bool SchemaValidated,
    string CodeLists,
    int DroppedFindings,
    DocumentSummary? Document,
    IReadOnlyList<Issue> Issues);
