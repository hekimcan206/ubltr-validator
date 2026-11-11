using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using UblTr.Validator;
using UblTr.Validator.Rules;

namespace UblTr.Cli;

/// <summary>
/// The command tree, kept separate from Program so tests can run it in-process with captured output.
/// Exit codes: 0 = every file valid, 1 = at least one file failed, 2 = usage or setup error.
/// </summary>
public static class CliApp
{
    public const int Valid = 0;
    public const int Invalid = 1;
    public const int UsageError = 2;

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, string workingDirectory)
    {
        var langOption = new Option<string>("--lang", "-l")
        {
            Description = "Output language: tr (default) or en / Çıktı dili: tr (varsayılan) veya en",
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable("UBLTR_LANG") ?? "tr",
        };
        langOption.AcceptOnlyFromAmong("tr", "en");

        var pathsArgument = new Argument<string[]>("paths")
        {
            Description = "XML files, directories (searched recursively for *.xml) or glob patterns such as 'invoices/**/*.xml'",
            Arity = ArgumentArity.OneOrMore,
        };
        var formatOption = new Option<string>("--format", "-f") { Description = "text, json or github", DefaultValueFactory = _ => "text" };
        formatOption.AcceptOnlyFromAmong("text", "json", "github");
        var kindOption = new Option<string>("--kind", "-k")
        {
            Description = "auto (from ProfileID), efatura or earsiv. Mirrors the 'type' parameter of GIB's Schematron.",
            DefaultValueFactory = _ => "auto",
        };
        kindOption.AcceptOnlyFromAmong("auto", "efatura", "earsiv");
        var gibDirOption = new Option<string?>("--gib-dir") { Description = $"Directory with the GIB xsdrt/ and schematron/ folders (default: ${GibAssets.EnvironmentVariable} or the copy next to the binary)" };
        var noSchemaOption = new Option<bool>("--no-schema") { Description = "Skip XSD validation and run business rules only" };
        var disableOption = new Option<string[]>("--disable") { Description = "Rule ids to suppress, e.g. --disable PTY-003 HDR-011", AllowMultipleArgumentsPerToken = true };
        var failOnOption = new Option<string>("--fail-on") { Description = "error (default) or warning", DefaultValueFactory = _ => "error" };
        failOnOption.AcceptOnlyFromAmong("error", "warning");
        var todayOption = new Option<string?>("--today") { Description = "Override today's date (yyyy-MM-dd) for the issue-date check; useful for reproducible runs" };

        var validate = new Command("validate", "Validate UBL-TR invoices / UBL-TR faturalarını doğrula")
        {
            pathsArgument, langOption, formatOption, kindOption, gibDirOption, noSchemaOption, disableOption, failOnOption, todayOption,
        };
        validate.SetAction(parse =>
        {
            var language = LanguageParser.Parse(parse.GetValue(langOption));
            var files = InputResolver.Resolve(parse.GetValue(pathsArgument) ?? [], workingDirectory);
            if (files.Count == 0)
            {
                stderr.WriteLine(language == Language.En ? "No XML files matched the given paths." : "Verilen yollarla eşleşen XML dosyası yok.");
                return UsageError;
            }

            var gibDir = parse.GetValue(gibDirOption) ?? GibAssets.DefaultDirectory();
            var assets = gibDir is null ? GibAssets.RulesOnly : GibAssets.Load(gibDir);
            var skipSchema = parse.GetValue(noSchemaOption);
            if (assets.Schemas is null && !skipSchema)
            {
                stderr.WriteLine(language == Language.En
                    ? "GIB UBL-TR schemas not found. Build the project (it downloads them), set UBLTR_GIB_DIR, pass --gib-dir, or use --no-schema."
                    : "GİB UBL-TR şemaları bulunamadı. Projeyi derleyin (şemaları indirir), UBLTR_GIB_DIR ayarlayın, --gib-dir verin ya da --no-schema kullanın.");
                return UsageError;
            }

            var options = new ValidationOptions
            {
                Kind = parse.GetValue(kindOption) switch { "efatura" => DocumentKind.EFatura, "earsiv" => DocumentKind.EArsiv, _ => DocumentKind.Auto },
                SkipSchema = skipSchema,
                DisabledRules = new HashSet<string>(parse.GetValue(disableOption) ?? [], StringComparer.OrdinalIgnoreCase),
            };
            if (parse.GetValue(todayOption) is { } today)
            {
                options = options with { Today = DateOnly.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture) };
            }

            var validator = new InvoiceValidator(assets);
            var reports = new ValidationReport[files.Count];
            Parallel.For(0, files.Count, i =>
                reports[i] = validator.Validate(files[i], options).ToReport(language, InputResolver.Display(files[i], workingDirectory)));

            var format = parse.GetValue(formatOption) switch { "json" => OutputFormat.Json, "github" => OutputFormat.GitHub, _ => OutputFormat.Text };
            Renderers.Write(stdout, format, reports, language);
            if (format == OutputFormat.GitHub && Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summary)
            {
                File.AppendAllText(summary, Renderers.StepSummary(reports, language));
            }

            var failOnWarning = parse.GetValue(failOnOption) == "warning";
            return reports.Any(r => !r.Valid || (failOnWarning && r.Warnings > 0)) ? Invalid : Valid;
        });

        var rulesFormat = new Option<string>("--format", "-f") { Description = "text or json", DefaultValueFactory = _ => "text" };
        rulesFormat.AcceptOnlyFromAmong("text", "json");
        var rules = new Command("rules", "List every rule with its origin / Tüm kuralları kaynağıyla listele") { langOption, rulesFormat };
        rules.SetAction(parse =>
        {
            var language = LanguageParser.Parse(parse.GetValue(langOption));
            if (parse.GetValue(rulesFormat) == "json")
            {
                stdout.WriteLine(JsonSerializer.Serialize(RuleCatalog.All.Select(r => Describe(r, language)), ReportJson.Options));
                return Valid;
            }

            foreach (var r in RuleCatalog.All)
            {
                stdout.WriteLine($"{r.Id,-8} {r.Severity,-7} {r.Origin,-13} {r.Title.In(language)}");
            }

            return Valid;
        });

        var ruleIdArgument = new Argument<string>("rule") { Description = "Rule id, e.g. HDR-010" };
        var explain = new Command("explain", "Explain one rule / Bir kuralı açıkla") { ruleIdArgument, langOption };
        explain.SetAction(parse =>
        {
            var language = LanguageParser.Parse(parse.GetValue(langOption));
            var id = parse.GetValue(ruleIdArgument)!;
            if (RuleCatalog.Find(id) is not { } rule)
            {
                stderr.WriteLine(language == Language.En ? $"Unknown rule '{id}'." : $"Bilinmeyen kural '{id}'.");
                return UsageError;
            }

            var en = language == Language.En;
            stdout.WriteLine($"{rule.Id}  {rule.Title.In(language)}");
            stdout.WriteLine($"{(en ? "Severity" : "Önem")}: {rule.Severity}   {(en ? "Category" : "Kategori")}: {rule.Category}   {(en ? "Origin" : "Kaynak")}: {rule.Origin}");
            stdout.WriteLine($"{(en ? "Source" : "Dayanak")}: {rule.Source}");
            stdout.WriteLine();
            stdout.WriteLine(rule.Explanation.In(language));
            return Valid;
        });

        var root = new RootCommand("ubltr - UBL-TR (e-Fatura / e-Arşiv) validator: XSD + business rules, Turkish and English explanations")
        {
            validate, rules, explain,
        };
        var invocation = new InvocationConfiguration { Output = stdout, Error = stderr };
        return root.Parse(args).Invoke(invocation);
    }

    private static object Describe(RuleDescriptor r, Language language) => new
    {
        r.Id,
        r.Category,
        r.Severity,
        r.Origin,
        r.Source,
        Title = r.Title.In(language),
        Explanation = r.Explanation.In(language),
    };
}
