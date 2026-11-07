using System.Text;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Output;

// Renders one finding as a labelled block, so a developer reading the terminal gets everything needed
// to act on it without opening the SARIF report:
//
//   ⚠ SEC006 · HSTS missing · warning
//     Where  App/Feature/App.Api/Program.cs:38
//     Why    UseHsts() is only called inside a development-only block, so production never sends
//            the Strict-Transport-Security header.
//     Fix    aspireforge fix SEC006
//     Docs   https://github.com/.../docs/rules/security.md#sec006---hsts-missing
//
// A finding about the project as a whole (something missing, rather than something at a line) shows
// "Scope  whole project" instead of pointing at the .csproj. "Fix" only appears when a single command
// resolves the finding (see RuleRemedies).
public static class FindingFormatter
{
    private const string Indent = "  ";
    private const int LabelWidth = 7;

    public static string Symbol(Severity severity) => severity switch
    {
        Severity.Error or Severity.Critical => "✗",
        Severity.Warning => "⚠",
        _ => "○",
    };

    public static IEnumerable<string> Format(
        AnalysisIssue issue,
        string? docsUrl,
        string? remedyCommand,
        string projectFile,
        string currentDirectory,
        int width)
    {
        yield return $"{Symbol(issue.Severity)} {issue.RuleId} · {issue.Title} · {issue.Severity.ToString().ToLowerInvariant()}";

        if (IsProjectWide(issue, projectFile))
        {
            yield return Label("Scope", "whole project");
        }
        else if (issue.FilePath is { Length: > 0 } file)
        {
            var location = DisplayPath(file, currentDirectory) + (issue.LineNumber is { } line ? $":{line}" : string.Empty);
            yield return Label("Where", location);
        }

        foreach (var line in Wrap(issue.Description, width - Indent.Length - LabelWidth).Select((text, i) => (text, i)))
        {
            yield return line.i == 0 ? Label("Why", line.text) : Indent + new string(' ', LabelWidth) + line.text;
        }

        if (remedyCommand is not null)
        {
            yield return Label("Fix", remedyCommand);
        }

        if (docsUrl is not null)
        {
            yield return Label("Docs", docsUrl);
        }
    }

    // Relative to where the command was run when the file is inside it; absolute otherwise (another
    // drive, or above the current directory), so the path can always be opened as printed.
    public static string DisplayPath(string file, string currentDirectory)
    {
        var relative = Path.GetRelativePath(currentDirectory, file);

        return Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal)
            ? file
            : relative.Replace('\\', '/');
    }

    // Greedy word wrap. A single word longer than the width (a URL, a long path) stays on its own line.
    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    // No line, and the "location" is just the analysed project's own .csproj (or nothing at all).
    public static bool IsProjectWide(AnalysisIssue issue, string projectFile) =>
        issue.LineNumber is null
        && (string.IsNullOrEmpty(issue.FilePath)
            || string.Equals(Path.GetFullPath(issue.FilePath), Path.GetFullPath(projectFile), StringComparison.OrdinalIgnoreCase));

    private static string Label(string label, string value) => Indent + label.PadRight(LabelWidth) + value;
}
