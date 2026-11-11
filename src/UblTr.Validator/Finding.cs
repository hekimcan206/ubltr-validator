using System.Xml;
using System.Xml.Linq;
using UblTr.Validator.Rules;

namespace UblTr.Validator;

/// <summary>
/// A language-neutral rule violation. It keeps the offending node so the location (line, column, path) is computed
/// only for documents that actually have findings.
/// </summary>
public sealed record Finding(RuleDescriptor Rule, XObject? Node, IReadOnlyList<object?> Args)
{
    /// <summary>Line/column for findings that have no node (XML parse errors, XSD errors).</summary>
    public int? ExplicitLine { get; init; }

    public int? ExplicitColumn { get; init; }

    public int? Line => ExplicitLine ?? (Node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : null);

    public int? Column => ExplicitColumn ?? (Node is IXmlLineInfo info && info.HasLineInfo() ? info.LinePosition : null);

    public string? Path => Node is null ? null : XmlPath.Of(Node);

    public Issue Localize(Language language) => new(
        Rule.Id,
        Rule.Severity,
        Rule.Category,
        Rule.Origin,
        Line,
        Column,
        Path,
        Rule.Title.In(language),
        Rule.FormatMessage(language, Args),
        Rule.Explanation.In(language),
        Rule.FormatFix(language, Args));
}

/// <summary>A finding rendered in one language, ready for JSON, text or GitHub annotations.</summary>
public sealed record Issue(
    string RuleId,
    Severity Severity,
    string Category,
    RuleOrigin Origin,
    int? Line,
    int? Column,
    string? Path,
    string Title,
    string Message,
    string Explanation,
    string Fix);

internal static class XmlPath
{
    /// <summary>Builds /Invoice/cac:InvoiceLine[3]/cbc:LineExtensionAmount style paths (1-based like XPath).</summary>
    public static string Of(XObject node)
    {
        var element = node as XElement ?? node.Parent;
        var segments = new Stack<string>();
        if (node is XAttribute attribute)
        {
            segments.Push("@" + attribute.Name.LocalName);
        }

        for (var e = element; e is not null; e = e.Parent)
        {
            var name = Ns.Qualified(e.Name);
            if (e.Parent is { } parent)
            {
                var index = 0;
                var count = 0;
                foreach (var sibling in parent.Elements(e.Name))
                {
                    count++;
                    if (ReferenceEquals(sibling, e))
                    {
                        index = count;
                    }
                }

                if (count > 1)
                {
                    name += $"[{index}]";
                }
            }

            segments.Push(name);
        }

        return "/" + string.Join('/', segments);
    }
}
