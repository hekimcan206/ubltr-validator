using System.Text;
using System.Text.Json;
using UblTr.Validator;
using UblTr.Validator.Rules;

namespace UblTr.Cli;

internal enum OutputFormat
{
    Text,
    Json,
    GitHub,
}

internal static class Renderers
{
    public static void Write(TextWriter output, OutputFormat format, IReadOnlyList<ValidationReport> reports, Language language)
    {
        switch (format)
        {
            case OutputFormat.Json:
                output.WriteLine(JsonSerializer.Serialize(reports, ReportJson.Options));
                break;
            case OutputFormat.GitHub:
                WriteGitHub(output, reports, language);
                break;
            default:
                WriteText(output, reports, language);
                break;
        }
    }

    private static void WriteText(TextWriter output, IReadOnlyList<ValidationReport> reports, Language language)
    {
        var en = language == Language.En;
        foreach (var report in reports)
        {
            var status = report.Valid
                ? (report.Warnings > 0 ? (en ? "OK (warnings)" : "GEÇERLİ (uyarılı)") : (en ? "OK" : "GEÇERLİ"))
                : (en ? "INVALID" : "GEÇERSİZ");
            output.WriteLine($"{status,-18} {report.Source}  "
                             + (en ? $"{report.Errors} error(s), {report.Warnings} warning(s)" : $"{report.Errors} hata, {report.Warnings} uyarı"));
            foreach (var issue in report.Issues)
            {
                var level = issue.Severity == Severity.Error ? (en ? "error" : "hata") : (en ? "warning" : "uyarı");
                var where = issue.Line is { } line ? $"{(en ? "line" : "satır")} {line}:{issue.Column}" : "";
                output.WriteLine($"  {level,-7} {issue.RuleId}  {where}  {issue.Title}");
                output.WriteLine($"          {issue.Message}");
                output.WriteLine($"          {(en ? "Why" : "Neden")}: {issue.Explanation}");
                output.WriteLine($"          {(en ? "Fix" : "Çözüm")}: {issue.Fix}");
                if (issue.Path is not null)
                {
                    output.WriteLine($"          {(en ? "Path" : "Yol")}: {issue.Path}");
                }
            }

            if (report.DroppedFindings > 0)
            {
                output.WriteLine(en
                    ? $"  ... {report.DroppedFindings} more finding(s) of the same rules not shown"
                    : $"  ... aynı kurallardan {report.DroppedFindings} bulgu daha gösterilmedi");
            }
        }

        var invalid = reports.Count(r => !r.Valid);
        output.WriteLine();
        output.WriteLine(en
            ? $"{reports.Count} file(s): {reports.Count - invalid} valid, {invalid} invalid"
            : $"{reports.Count} dosya: {reports.Count - invalid} geçerli, {invalid} geçersiz");
    }

    /// <summary>GitHub Actions workflow commands: each issue becomes an inline annotation on the file and line.</summary>
    private static void WriteGitHub(TextWriter output, IReadOnlyList<ValidationReport> reports, Language language)
    {
        foreach (var report in reports)
        {
            foreach (var issue in report.Issues)
            {
                output.WriteLine(Annotation(report, issue, language));
            }
        }

        WriteText(output, reports, language);
    }

    internal static string Annotation(ValidationReport report, Issue issue, Language language)
    {
        var command = issue.Severity == Severity.Error ? "error" : "warning";
        var props = new StringBuilder($"file={Property(report.Source ?? "")}");
        if (issue.Line is { } line)
        {
            props.Append($",line={line}");
            if (issue.Column is { } col)
            {
                props.Append($",col={col}");
            }
        }

        props.Append($",title={Property($"{issue.RuleId} {issue.Title}")}");
        var fixLabel = language == Language.En ? "Fix" : "Çözüm";
        return $"::{command} {props}::{Data($"{issue.Message}\n{fixLabel}: {issue.Fix}")}";
    }

    /// <summary>Markdown summary for $GITHUB_STEP_SUMMARY.</summary>
    public static string StepSummary(IReadOnlyList<ValidationReport> reports, Language language)
    {
        var en = language == Language.En;
        var sb = new StringBuilder();
        sb.AppendLine(en ? "### UBL-TR validation" : "### UBL-TR doğrulama");
        sb.AppendLine();
        sb.AppendLine(en ? "| File | Result | Errors | Warnings |" : "| Dosya | Sonuç | Hata | Uyarı |");
        sb.AppendLine("|---|---|---:|---:|");
        foreach (var r in reports)
        {
            var result = r.Valid ? (en ? "valid" : "geçerli") : (en ? "**invalid**" : "**geçersiz**");
            sb.AppendLine($"| `{r.Source}` | {result} | {r.Errors} | {r.Warnings} |");
        }

        return sb.ToString();
    }

    private static string Data(string value) =>
        value.Replace("%", "%25", StringComparison.Ordinal).Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string Property(string value) =>
        Data(value).Replace(":", "%3A", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal);
}
