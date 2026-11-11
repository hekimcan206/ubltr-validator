using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using UblTr.Validator.Rules;
using UblTr.Validator.Xml;

namespace UblTr.Validator;

/// <summary>
/// Validates UBL-TR invoices: well-formedness and security, XSD and business rules, in a single parse. The schema
/// validation runs inside the XmlReader that feeds XDocument.Load, so a document is read exactly once.
/// Thread-safe: one instance can serve concurrent requests (the compiled XmlSchemaSet is only read).
/// </summary>
public sealed class InvoiceValidator(GibAssets assets)
{
    public static readonly ActivitySource ActivitySource = new("UblTr.Validator");

    /// <summary>Hard cap on XSD findings per document: after the first few, schema errors cascade and add nothing.</summary>
    private const int MaxSchemaFindings = 50;

    public GibAssets Assets { get; } = assets;

    public ValidationResult Validate(string path, ValidationOptions? options = null)
    {
        using var stream = File.OpenRead(path);
        return Validate(stream, options);
    }

    public ValidationResult Validate(Stream stream, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= new ValidationOptions();
        using var activity = ActivitySource.StartActivity("ubltr.validate");

        var counting = new CountingStream(stream);
        var findings = new List<Finding>();
        var schemaFindings = 0;
        var useSchema = !options.SkipSchema && Assets.Schemas is not null;

        var settings = new XmlReaderSettings
        {
            // UBL-TR never uses a DTD; prohibiting it closes XXE and entity-expansion attacks.
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = false,
        };

        if (useSchema)
        {
            settings.ValidationType = ValidationType.Schema;
            settings.Schemas = Assets.Schemas!;
            // xsi:schemaLocation in the document is ignored: the document must not choose its own schema.
            settings.ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints;
            settings.ValidationEventHandler += (_, e) =>
            {
                if (e.Severity != XmlSeverityType.Error || ++schemaFindings > MaxSchemaFindings)
                {
                    return;
                }

                var (message, fix) = XsdMessageTranslator.Translate(e.Message);
                findings.Add(new Finding(RuleCatalog.SchemaViolation, null, [message, fix])
                {
                    ExplicitLine = e.Exception?.LineNumber,
                    ExplicitColumn = e.Exception?.LinePosition,
                });
            };
        }

        XDocument document;
        try
        {
            using var reader = XmlReader.Create(counting, settings);
            document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            var rule = ex.Message.Contains("DTD", StringComparison.Ordinal) ? RuleCatalog.DtdForbidden : RuleCatalog.MalformedXml;
            findings.Clear();
            findings.Add(new Finding(rule, null, [ex.Message]) { ExplicitLine = ex.LineNumber, ExplicitColumn = ex.LinePosition });
            return Finish(null, findings, options, useSchema, counting.BytesRead, activity);
        }

        var root = document.Root!;
        if (root.Name != Ns.Invoice + "Invoice")
        {
            findings.Clear();
            var hint = root.Name.Namespace == Ns.Sbd
                ? new LocalizedText("Bu bir StandardBusinessDocument zarfı; içindeki Invoice elemanını ayrı bir dosya olarak doğrulayın.",
                    "This is a StandardBusinessDocument envelope; validate the Invoice inside it as a separate file.")
                : LocalizedText.Same("");
            findings.Add(new Finding(RuleCatalog.NotAnInvoice, root, [root.Name.ToString(), hint]));
            return Finish(null, findings, options, useSchema, counting.BytesRead, activity);
        }

        var context = new InvoiceContext(root, options, Assets.CodeLists, findings);
        HeaderChecks.Run(context);
        PartyChecks.Run(context);
        CurrencyChecks.Run(context);

        var summary = new DocumentSummary(
            InvoiceContext.Text(root, Ns.Cbc + "ID"),
            InvoiceContext.Text(root, Ns.Cbc + "UUID"),
            context.ProfileId,
            context.InvoiceTypeCode,
            context.Currency,
            context.Kind,
            context.Lines.Count);
        return Finish(summary, findings, options, useSchema, counting.BytesRead, activity);
    }

    private ValidationResult Finish(DocumentSummary? summary, List<Finding> findings, ValidationOptions options, bool schemaValidated,
        long bytes, Activity? activity)
    {
        var kept = findings;
        if (options.DisabledRules.Count > 0 || findings.Count > options.MaxFindingsPerRule)
        {
            kept = [.. findings
                .Where(f => !options.DisabledRules.Contains(f.Rule.Id))
                .GroupBy(f => f.Rule.Id)
                .SelectMany(g => g.Take(options.MaxFindingsPerRule))];
        }

        var dropped = findings.Count - kept.Count - findings.Count(f => options.DisabledRules.Contains(f.Rule.Id));
        var result = new ValidationResult(summary, kept, schemaValidated, Assets.CodeLists.Source, bytes, dropped);
        activity?.SetTag("ubltr.valid", result.IsValid);
        activity?.SetTag("ubltr.errors", result.ErrorCount);
        activity?.SetTag("ubltr.bytes", bytes);
        return result;
    }
}
