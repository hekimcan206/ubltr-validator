using System.Text.RegularExpressions;
using UblTr.Validator;
using UblTr.Validator.Rules;

namespace UblTr.Tests;

public sealed partial class RuleCatalogTests
{
    public static TheoryData<string> RuleIds => [.. RuleCatalog.All.Select(r => r.Id)];

    [GeneratedRegex("^[A-Z]{3}-[0-9]{3}$")]
    private static partial Regex IdPattern();

    [Fact]
    public void Rule_ids_are_unique_and_well_formed()
    {
        RuleCatalog.All.Select(r => r.Id).Should().OnlyHaveUniqueItems();
        RuleCatalog.All.Should().AllSatisfy(r => IdPattern().IsMatch(r.Id).Should().BeTrue(r.Id));
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Every_rule_explains_itself_and_suggests_a_fix_in_both_languages(string id)
    {
        var rule = RuleCatalog.Find(id)!;
        foreach (var text in new[] { rule.Title, rule.Explanation, rule.Message, rule.Fix })
        {
            text.Tr.Should().NotBeNullOrWhiteSpace();
            text.En.Should().NotBeNullOrWhiteSpace();
        }

        rule.Title.Tr.Should().NotBe(rule.Title.En, "the Turkish title must be a translation, not a copy");
        rule.Explanation.Tr.Should().NotBe(rule.Explanation.En);
        rule.Source.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Message_templates_format_with_any_argument_count_the_rule_uses(string id)
    {
        var rule = RuleCatalog.Find(id)!;
        var args = Enumerable.Range(0, 6).Select(i => (object?)$"a{i}").ToArray();
        foreach (var language in new[] { Language.Tr, Language.En })
        {
            rule.FormatMessage(language, args).Should().NotContain("{0}");
            rule.FormatFix(language, args).Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Gib_schematron_rules_name_the_abstract_rule_they_reimplement()
    {
        RuleCatalog.All.Where(r => r.Origin == RuleOrigin.GibSchematron)
            .Should().AllSatisfy(r => r.Source.Should().Contain("Schematron").And.Contain("Check"));
    }

    [Theory]
    [InlineData("tr", Language.Tr)]
    [InlineData("EN", Language.En)]
    [InlineData("en-US", Language.En)]
    [InlineData(null, Language.Tr)]
    [InlineData("de", Language.Tr)]
    public void Language_parsing_falls_back_to_turkish(string? input, Language expected) =>
        LanguageParser.Parse(input).Should().Be(expected);

    [Fact]
    public void Localized_argument_is_rendered_in_the_requested_language()
    {
        var args = new object?[] { new LocalizedText("Satıcı", "Supplier"), "123" };
        RuleCatalog.VknLength.FormatMessage(Language.Tr, args).Should().StartWith("Satıcı VKN");
        RuleCatalog.VknLength.FormatMessage(Language.En, args).Should().StartWith("Supplier VKN");
    }
}
