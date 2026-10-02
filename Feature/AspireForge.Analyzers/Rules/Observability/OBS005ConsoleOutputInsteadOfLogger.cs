using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

// Console.WriteLine in a web app's code bypasses ILogger: no log level, no category, no structured
// properties, no trace correlation, and none of the configured sinks (OpenTelemetry, Serilog, ...)
// ever see it. Scoped to web projects - a console tool or CLI legitimately writes to the console.
// Covers Console.Write/WriteLine and their Console.Error variants; comments are stripped first.
public sealed partial class OBS005ConsoleOutputInsteadOfLogger : IAnalysisRule
{
    private const int MaxLocationsInDescription = 3;

    public string Id => "OBS005";

    public string Title => "Console output instead of ILogger";

    public Severity DefaultSeverity => Severity.Suggestion;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        var findings = new List<(string File, int Line)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match call in ConsoleOutputCall().Matches(code))
            {
                findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index)));
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        var locations = string.Join(
            ", ",
            findings.Take(MaxLocationsInDescription).Select(finding => $"{Path.GetFileName(finding.File)}:{finding.Line}"));
        var more = findings.Count > MaxLocationsInDescription
            ? $" (and {findings.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"Console.Write/WriteLine is used in application code ({locations}{more}). Console output has no log level, "
                + "category, or trace correlation and never reaches your configured log sinks - inject ILogger<T> instead.",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    [GeneratedRegex(@"\bConsole\s*\.\s*(?:Error\s*\.\s*)?Write(?:Line)?\s*\(")]
    private static partial Regex ConsoleOutputCall();
}
