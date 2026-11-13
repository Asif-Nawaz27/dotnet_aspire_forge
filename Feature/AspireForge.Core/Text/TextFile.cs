using System.Text;

namespace AspireForge.Core.Text;

// Line-by-line edits that leave the rest of the file alone: its line endings, final newline, and BOM. Shared
// by `fix` (through FixWorkspace) and by `add`/`update`, which used to rewrite every line ending with
// File.WriteAllLines - so a one-line change showed up as the whole file changing.
public static class TextFile
{
    public sealed record Content(string Text, bool HasBom);

    public static Content Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);

        return new Content(Encoding.UTF8.GetString(hasBom ? bytes[Encoding.UTF8.Preamble.Length..] : bytes), hasBom);
    }

    public static void Write(string path, string text, bool hasBom) =>
        File.WriteAllText(path, text, new UTF8Encoding(hasBom));

    // The edit sees the lines without their endings. A file that ends with a newline has an empty last
    // line, so the final newline survives. A file that mixes endings comes back with CRLF throughout.
    public static string EditLines(string text, Action<List<string>> edit)
    {
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();

        edit(lines);

        return string.Join(newLine, lines);
    }

    // Writes only when the edit changed something.
    public static void Edit(string path, Action<List<string>> edit)
    {
        var content = Read(path);
        var edited = EditLines(content.Text, edit);

        if (edited != content.Text)
        {
            Write(path, edited, content.HasBom);
        }
    }
}
