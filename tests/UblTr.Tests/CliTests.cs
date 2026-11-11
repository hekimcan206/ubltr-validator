using System.Text.Json;
using UblTr.Cli;
using UblTr.Tests.Support;

namespace UblTr.Tests;

public sealed class CliTests
{
    private static readonly string[] FixedToday = ["--today", "2026-11-11"];

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = CliApp.Run([.. args, .. args.Length > 0 && args[0] == "validate" ? FixedToday : []],
            stdout, stderr, TestAssets.RepoRoot);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Valid_directory_exits_zero()
    {
        var (code, output, _) = Run("validate", "samples/invoices/valid");
        code.Should().Be(CliApp.Valid);
        output.Should().Contain("8 dosya: 8 geçerli, 0 geçersiz");
    }

    [Fact]
    public void Invalid_file_exits_one_with_turkish_explanation_and_fix()
    {
        var (code, output, _) = Run("validate", "samples/invoices/invalid/HDR-010.xml");
        code.Should().Be(CliApp.Invalid);
        output.Should().Contain("HDR-010").And.Contain("Neden:").And.Contain("Çözüm:");
    }

    [Fact]
    public void English_output_on_request()
    {
        var (_, output, _) = Run("validate", "samples/invoices/invalid/TOT-005.xml", "--lang", "en");
        output.Should().Contain("Payable amount is wrong").And.Contain("Fix:");
    }

    [Fact]
    public void Warnings_pass_by_default_and_fail_with_fail_on_warning()
    {
        Run("validate", "samples/invoices/invalid/HDR-011.xml").Code.Should().Be(CliApp.Valid);
        Run("validate", "samples/invoices/invalid/HDR-011.xml", "--fail-on", "warning").Code.Should().Be(CliApp.Invalid);
    }

    [Fact]
    public void Json_output_is_a_parseable_report_array()
    {
        var (code, output, _) = Run("validate", "samples/invoices/invalid/CUR-002.xml", "samples/invoices/valid/ticari-satis-usd.xml", "-f", "json");
        code.Should().Be(CliApp.Invalid);
        using var json = JsonDocument.Parse(output);
        json.RootElement.GetArrayLength().Should().Be(2);
        json.RootElement[0].GetProperty("issues")[0].GetProperty("ruleId").GetString().Should().Be("CUR-002");
        json.RootElement[0].GetProperty("issues")[0].GetProperty("severity").GetString().Should().Be("error");
        json.RootElement[1].GetProperty("valid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Github_format_emits_escaped_workflow_annotations()
    {
        var (_, output, _) = Run("validate", "samples/invoices/invalid/PTY-002.xml", "--format", "github", "--lang", "en");
        var annotation = output.Split('\n').First(l => l.StartsWith("::error ", StringComparison.Ordinal));
        annotation.Should().StartWith("::error file=samples/invoices/invalid/PTY-002.xml,line=");
        annotation.Should().Contain("title=PTY-002 VKN must have 10 digits::");
        annotation.Should().Contain("%0AFix: ");
    }

    [Fact]
    public void Globs_and_disable_work_together()
    {
        var (code, output, _) = Run("validate", "samples/invoices/invalid/PTY-*.xml",
            "--disable", "PTY-001", "PTY-002", "PTY-003", "PTY-004", "PTY-005", "PTY-006", "PTY-007", "PTY-008");

        code.Should().Be(CliApp.Valid);
        output.Should().Contain("8 dosya: 8 geçerli, 0 geçersiz");
    }

    [Fact]
    public void No_matching_files_is_a_usage_error()
    {
        var (code, _, err) = Run("validate", "does/not/exist/*.xml");
        code.Should().Be(CliApp.UsageError);
        err.Should().Contain("XML");
    }

    [Fact]
    public void Rules_command_lists_every_rule_as_json()
    {
        var (code, output, _) = Run("rules", "--format", "json", "--lang", "en");
        code.Should().Be(0);
        using var json = JsonDocument.Parse(output);
        json.RootElement.GetArrayLength().Should().Be(Validator.Rules.RuleCatalog.All.Count);
    }

    [Fact]
    public void Explain_prints_the_rule_and_rejects_unknown_ids()
    {
        Run("explain", "tax-009").Out.Should().Contain("TAX-009").And.Contain("Tevkifat");
        Run("explain", "NOPE-1").Code.Should().Be(CliApp.UsageError);
    }

    [Fact]
    public void Missing_schemas_without_no_schema_is_a_usage_error()
    {
        var empty = Directory.CreateTempSubdirectory().FullName;
        var (code, _, err) = Run("validate", "samples/invoices/valid/temel-satis-try.xml", "--gib-dir", empty, "--lang", "en");
        code.Should().Be(CliApp.UsageError);
        err.Should().Contain("schemas not found");
        Run("validate", "samples/invoices/valid/temel-satis-try.xml", "--gib-dir", empty, "--no-schema").Code.Should().Be(CliApp.Valid);
    }
}
