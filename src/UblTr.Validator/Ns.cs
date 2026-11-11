using System.Xml.Linq;

namespace UblTr.Validator;

/// <summary>UBL 2.1 namespaces used by UBL-TR invoices.</summary>
public static class Ns
{
    public static readonly XNamespace Invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    public static readonly XNamespace Sbd = "http://www.unece.org/cefact/namespaces/StandardBusinessDocumentHeader";

    /// <summary>Short prefix for paths and messages ("cbc:ID" reads better than a URN).</summary>
    public static string Prefix(XNamespace ns) =>
        ns == Cbc ? "cbc" : ns == Cac ? "cac" : ns == Ext ? "ext" : ns == Invoice ? "" : ns == Sbd ? "sh" : "";

    public static string Qualified(XName name)
    {
        var prefix = Prefix(name.Namespace);
        return prefix.Length == 0 ? name.LocalName : $"{prefix}:{name.LocalName}";
    }
}
