using System.Diagnostics;
using System.Xml;
using System.Xml.Schema;
using UblTr.Validator.CodeLists;
using UblTr.Validator.Xml;

namespace UblTr.Validator;

/// <summary>
/// The GIB artefacts the validator needs at run time: the compiled UBL-TR XSD set and the code lists. They are loaded
/// once per process (compiling the XSDs is the expensive part) and shared by every validation.
/// </summary>
public sealed class GibAssets
{
    public const string EnvironmentVariable = "UBLTR_GIB_DIR";
    private const string InvoiceSchema = "xsdrt/maindoc/UBL-Invoice-2.1.xsd";
    private const string Codelist = "schematron/UBL-TR_Codelist.xml";

    private GibAssets(XmlSchemaSet? schemas, CodeListSet codeLists, string? directory, TimeSpan schemaCompileTime)
    {
        Schemas = schemas;
        CodeLists = codeLists;
        Directory = directory;
        SchemaCompileTime = schemaCompileTime;
    }

    /// <summary>Compiled schema set, or null when the XSDs were not found (validation then needs SkipSchema).</summary>
    public XmlSchemaSet? Schemas { get; }

    public CodeListSet CodeLists { get; }

    public string? Directory { get; }

    public TimeSpan SchemaCompileTime { get; }

    /// <summary>Assets without XSDs and with the built-in code list snapshot (rules only).</summary>
    public static GibAssets RulesOnly { get; } = new(null, CodeListSet.BuiltIn, null, TimeSpan.Zero);

    /// <summary>Default lookup: $UBLTR_GIB_DIR, then the gib/ folder the build copies next to the binaries.</summary>
    public static string? DefaultDirectory()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        var nextToBinaries = Path.Combine(AppContext.BaseDirectory, "gib");
        return System.IO.Directory.Exists(nextToBinaries) ? nextToBinaries : null;
    }

    public static GibAssets LoadDefault() => DefaultDirectory() is { } dir ? Load(dir) : RulesOnly;

    /// <summary>Loads XSDs and code lists from a directory laid out as xsdrt/... and schematron/....</summary>
    public static GibAssets Load(string directory)
    {
        var dir = Path.GetFullPath(directory);
        var schemaPath = Path.Combine(dir, InvoiceSchema);
        XmlSchemaSet? schemas = null;
        var stopwatch = Stopwatch.StartNew();
        if (File.Exists(schemaPath))
        {
            var resolver = new DirectoryBoundResolver(dir);
            schemas = new XmlSchemaSet { XmlResolver = resolver };
            // The UBL copy of the W3C xmldsig schema declares entities in an internal DTD subset, and XmlSchemaSet
            // silently drops imports it cannot read with its own (DTD-prohibiting) settings. So every schema file is
            // pre-loaded here with an internal-subset-only policy: external entities can only resolve inside the
            // schema directory and entity expansion is capped. Invoice documents themselves never get a DTD.
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = resolver,
                MaxCharactersFromEntities = 64 * 1024,
            };
            foreach (var xsd in System.IO.Directory.EnumerateFiles(Path.Combine(dir, "xsdrt", "common"), "*.xsd").Order(StringComparer.Ordinal)
                         .Append(schemaPath))
            {
                using var reader = XmlReader.Create(xsd, readerSettings);
                schemas.Add(null, reader);
            }

            schemas.Compile();
        }

        stopwatch.Stop();
        var codelistPath = Path.Combine(dir, Codelist);
        var codeLists = File.Exists(codelistPath) ? CodeListSet.LoadGibCodelist(codelistPath) : CodeListSet.BuiltIn;
        return new GibAssets(schemas, codeLists, dir, stopwatch.Elapsed);
    }
}
