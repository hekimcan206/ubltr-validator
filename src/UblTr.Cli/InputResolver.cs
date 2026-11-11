using Microsoft.Extensions.FileSystemGlobbing;

namespace UblTr.Cli;

/// <summary>Expands CLI inputs: files as given, directories recursively (*.xml), anything else as a glob.</summary>
internal static class InputResolver
{
    private static readonly char[] Separators = ['\n', ','];

    public static IReadOnlyList<string> Resolve(IEnumerable<string> inputs, string workingDirectory)
    {
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var raw in inputs.SelectMany(i => i.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            var full = Path.GetFullPath(raw, workingDirectory);
            if (File.Exists(full))
            {
                files.Add(full);
            }
            else if (Directory.Exists(full))
            {
                foreach (var f in Directory.EnumerateFiles(full, "*.xml", SearchOption.AllDirectories))
                {
                    files.Add(Path.GetFullPath(f));
                }
            }
            else
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(raw.Replace('\\', '/'));
                foreach (var f in matcher.GetResultsInFullPath(workingDirectory))
                {
                    files.Add(Path.GetFullPath(f));
                }
            }
        }

        return [.. files];
    }

    /// <summary>Path relative to the working directory with forward slashes (what GitHub annotations expect).</summary>
    public static string Display(string path, string workingDirectory) =>
        Path.GetRelativePath(workingDirectory, path).Replace('\\', '/');
}
