using System.Xml.Linq;
using UblTr.SampleGen;
using UblTr.Tests.Support;
using UblTr.Validator;

namespace UblTr.Tests;

/// <summary>Real schema errors produced by System.Xml against the GIB XSDs, rendered in both languages.</summary>
public sealed class XsdMessageTranslatorTests
{
    private static Issue[] SchemaIssues(Action<XElement> mutate, Language language)
    {
        var doc = InvoiceWriter.ToXml(SampleCatalog.Basic);
        mutate(doc.Root!);
        return [.. TestAssets.Validate(InvoiceWriter.Serialize(doc)).ToReport(language).Issues.Where(i => i.RuleId == "XSD-001")];
    }

    [Fact]
    public void Invalid_decimal_explains_the_separator()
    {
        var issue = SchemaIssues(r => r.Element(Ns.Cac + "LegalMonetaryTotal")!.Element(Ns.Cbc + "PayableAmount")!.Value = "1.234,50", Language.Tr).Single();
        issue.Message.Should().Contain("cbc:PayableAmount").And.Contain("'1.234,50'");
        issue.Fix.Should().Contain("Ondalık ayıracı nokta");
    }

    [Fact]
    public void Invalid_date_explains_the_format()
    {
        var issue = SchemaIssues(r => r.Element(Ns.Cbc + "IssueDate")!.Value = "02.11.2026", Language.En).Single();
        issue.Message.Should().Contain("'02.11.2026'");
        issue.Fix.Should().Contain("YYYY-MM-DD");
    }

    [Fact]
    public void Missing_currency_attribute_suggests_currencyID()
    {
        var issue = SchemaIssues(r => r.Element(Ns.Cac + "LegalMonetaryTotal")!.Element(Ns.Cbc + "PayableAmount")!.Attribute("currencyID")!.Remove(), Language.Tr)
            .Single();
        issue.Message.Should().Contain("currencyID");
        issue.Fix.Should().Contain("currencyID=\"TRY\"");
    }

    [Fact]
    public void Misplaced_element_lists_what_was_expected_with_prefixes()
    {
        var issue = SchemaIssues(r =>
        {
            var note = r.Element(Ns.Cbc + "Note")!;
            note.Remove();
            r.Element(Ns.Cbc + "UUID")!.AddBeforeSelf(note);
        }, Language.En).First();
        issue.Message.Should().Contain("Unexpected element 'cbc:Note'").And.Contain("cbc:UUID");
        issue.Message.Should().NotContain("urn:oasis");
    }

    [Fact]
    public void Undeclared_attribute_is_named()
    {
        var issue = SchemaIssues(r => r.Element(Ns.Cbc + "ID")!.SetAttributeValue("foo", "bar"), Language.Tr).Single();
        issue.Message.Should().Contain("'foo'");
    }

    [Fact]
    public void Schema_errors_carry_line_and_column()
    {
        var issue = SchemaIssues(r => r.Element(Ns.Cbc + "IssueDate")!.Value = "yesterday", Language.Tr).Single();
        issue.Line.Should().BeGreaterThan(1);
        issue.Column.Should().BeGreaterThan(0);
    }
}
