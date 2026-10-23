using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

// Log messages built from values at the call site - $"User {id} signed in", "User " + id, or
// string.Format - instead of a message template with named placeholders:
//   - Structure is lost. With LogInformation("User {UserId} signed in", id) the sink stores UserId as a
//     field you can filter and aggregate on; an interpolated message is just a unique string per call.
//   - The string is built even when the level is disabled, allocating on every call for nothing.
//   - Every message text is distinct, so log-based alerting and grouping by message stops working.
// The same as code analysis rule CA2254 ("Template should be a static expression").
//
// Calls checked - the message template is the first argument that's a string expression, so
// LogError(ex, $"...") and Log(LogLevel.Warning, eventId, $"...") are covered:
//   - Microsoft.Extensions.Logging: LogTrace/Debug/Information/Warning/Error/Critical, Log, BeginScope
//   - Serilog (files with `using Serilog`): Verbose/Debug/Information/Warning/Error/Fatal
//   - NLog (files with `using NLog`): Trace/Debug/Info/Warn/Error/Fatal
// Interpolated strings without holes, {{escaped}} braces, and constant concatenation are fine.
public sealed partial class OBS006InterpolatedLogMessage : IAnalysisRule
{
    private const int MaxLocationsInDescription = 3;

    public string Id => "OBS006";

    public string Title => "Log message built from values";

    public Severity DefaultSeverity => Severity.Suggestion;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line, string Method)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);
            var calls = MicrosoftLoggingCall().Matches(code).AsEnumerable();

            if (UsingSerilog().IsMatch(code))
            {
                calls = calls.Concat(SerilogCall().Matches(code));
            }

            if (UsingNLog().IsMatch(code))
            {
                calls = calls.Concat(NLogCall().Matches(code));
            }

            foreach (var call in calls)
            {
                var arguments = SourceFileHeuristics.ReadCallArguments(code, call.Index + call.Length);
                var message = StringExpressions.SplitArguments(arguments).FirstOrDefault(StringExpressions.IsStringExpression);

                if (message is not null && StringExpressions.IsBuiltFromValues(message))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index), call.Groups["method"].Value));
                }
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings
            .Distinct()
            .OrderBy(f => f.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Line)
            .ToList();

        var locations = string.Join(
            ", ",
            findings.Take(MaxLocationsInDescription).Select(f => $"{f.Method} at {Path.GetFileName(f.File)}:{f.Line}"));
        var more = findings.Count > MaxLocationsInDescription
            ? $" (and {findings.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"Log messages are built with interpolation, concatenation, or string.Format ({locations}{more}). The values "
                + "become part of the text instead of searchable fields, and the string is built even when the level is "
                + "off. Use a message template with named placeholders: LogInformation(\"User {UserId} signed in\", userId).",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    [GeneratedRegex(@"\.(?<method>Log(?:Trace|Debug|Information|Warning|Error|Critical)?|BeginScope)\s*(?:<[^<>()]*>)?\s*\(")]
    private static partial Regex MicrosoftLoggingCall();

    [GeneratedRegex(@"\.(?<method>Verbose|Debug|Information|Warning|Error|Fatal)\s*(?:<[^<>()]*>)?\s*\(")]
    private static partial Regex SerilogCall();

    [GeneratedRegex(@"\.(?<method>Trace|Debug|Info|Warn|Error|Fatal)\s*\(")]
    private static partial Regex NLogCall();

    [GeneratedRegex(@"^\s*using\s+Serilog\s*;", RegexOptions.Multiline)]
    private static partial Regex UsingSerilog();

    [GeneratedRegex(@"^\s*using\s+NLog\s*;", RegexOptions.Multiline)]
    private static partial Regex UsingNLog();
}
