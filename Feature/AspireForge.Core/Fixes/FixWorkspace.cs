using System.Text;
using AspireForge.Core.Features;

namespace AspireForge.Core.Fixes;

public sealed record FileChange(string Path, string Before, string After);

// Where fixes make their edits. Nothing reaches the disk until Commit, which is what makes both of these
// possible: --dry-run shows Changes and stops, and when one of several fixes fails nothing is written,
// rather than leaving the project half-fixed.
//
// Edits keep each file's line endings, final newline, and BOM, so the only differences in a diff are the
// lines a fix actually changed.
public sealed class FixWorkspace(FeatureContext context)
{
    private readonly Dictionary<string, WorkspaceFile> _files = [];

    public FeatureContext Context { get; } = context;

    public IReadOnlyList<FileChange> Changes =>
        _files
            .Where(file => file.Value.Original != file.Value.Current)
            .OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => new FileChange(file.Key, file.Value.Original, file.Value.Current))
            .ToList();

    // An edit that throws leaves the file as it was.
    public void Edit(string path, Action<List<string>> edit)
    {
        var file = Load(path);
        var newLine = file.Current.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = file.Current.Split('\n').Select(line => line.TrimEnd('\r')).ToList();

        edit(lines);

        file.Current = string.Join(newLine, lines);
    }

    public void Commit()
    {
        foreach (var (path, file) in _files.Where(file => file.Value.Original != file.Value.Current))
        {
            File.WriteAllText(path, file.Current, new UTF8Encoding(file.HasBom));
        }
    }

    private WorkspaceFile Load(string path)
    {
        if (_files.TryGetValue(path, out var file))
        {
            return file;
        }

        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var text = Encoding.UTF8.GetString(hasBom ? bytes[Encoding.UTF8.Preamble.Length..] : bytes);

        return _files[path] = new WorkspaceFile(text, hasBom);
    }

    private sealed class WorkspaceFile(string text, bool hasBom)
    {
        public string Original { get; } = text;

        public bool HasBom { get; } = hasBom;

        public string Current { get; set; } = text;
    }
}
