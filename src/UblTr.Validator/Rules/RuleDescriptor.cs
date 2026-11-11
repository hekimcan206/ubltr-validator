using System.Globalization;

namespace UblTr.Validator.Rules;

public enum Severity
{
    Warning,
    Error,
}

/// <summary>What a rule is built from, so a reader can tell official GIB checks from this project's own additions.</summary>
public enum RuleOrigin
{
    /// <summary>Re-implements an assertion of GIB's published Schematron (the source names the abstract rule).</summary>
    GibSchematron,

    /// <summary>Follows from a GIB code list or guide, but GIB's Schematron does not assert it.</summary>
    GibDerived,

    /// <summary>Arithmetic or identifier consistency that no GIB Schematron rule checks (totals, checksums).</summary>
    Consistency,

    /// <summary>XML well-formedness, security and XSD validation.</summary>
    Structure,
}

/// <summary>
/// Static metadata of a rule. Message and fix templates use composite-format placeholders ({0}, {1}, ...) that are
/// filled from the finding's arguments; an argument may itself be a <see cref="LocalizedText"/>.
/// </summary>
public sealed record RuleDescriptor(
    string Id,
    string Category,
    Severity Severity,
    RuleOrigin Origin,
    string Source,
    LocalizedText Title,
    LocalizedText Explanation,
    LocalizedText Message,
    LocalizedText Fix)
{
    public string FormatMessage(Language language, IReadOnlyList<object?> args) => Format(Message.In(language), language, args);

    public string FormatFix(Language language, IReadOnlyList<object?> args) => Format(Fix.In(language), language, args);

    private static string Format(string template, Language language, IReadOnlyList<object?> args)
    {
        if (args.Count == 0)
        {
            return template;
        }

        var resolved = args.Select(a => a switch
        {
            LocalizedText text => text.In(language),
            null => language == Language.En ? "(missing)" : "(yok)",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => a.ToString(),
        }).ToArray<object?>();
        return string.Format(CultureInfo.InvariantCulture, template, resolved);
    }
}
