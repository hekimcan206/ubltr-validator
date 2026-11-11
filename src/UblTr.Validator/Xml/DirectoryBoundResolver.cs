using System.Xml;

namespace UblTr.Validator.Xml;

/// <summary>
/// Resolves xsd:import/xsd:include only to files inside the schema directory. The default XmlUrlResolver would follow
/// any URL (including http and UNC paths) that a tampered schema file names.
/// </summary>
internal sealed class DirectoryBoundResolver(string root) : XmlUrlResolver
{
    private readonly string _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        if (!absoluteUri.IsFile)
        {
            throw new XmlException($"Schema reference '{absoluteUri}' is not a local file; only files under the schema directory are allowed.");
        }

        var path = Path.GetFullPath(absoluteUri.LocalPath);
        if (!path.StartsWith(_root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new XmlException($"Schema reference '{path}' is outside the schema directory.");
        }

        return base.GetEntity(absoluteUri, role, ofObjectToReturn);
    }
}
