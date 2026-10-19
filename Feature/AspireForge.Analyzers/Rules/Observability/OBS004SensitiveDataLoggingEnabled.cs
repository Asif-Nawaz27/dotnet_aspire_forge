using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

// EF Core's EnableSensitiveDataLogging() writes query parameter values - emails, names, tokens,
// anything a query filters or saves - into logs and exception messages. Useful locally; in production
// it copies personal data into every log sink, which is both a privacy breach and often a compliance
// one (GDPR, PCI).
//
// Each call is flagged unless something limits it to development. Recognised guards:
//   - the bool overload with an environment check: EnableSensitiveDataLogging(env.IsDevelopment())
//   - an explicit EnableSensitiveDataLogging(false)
//   - an enclosing `if (...IsDevelopment()...)` - braced or braceless - that isn't negated
//   - an enclosing `#if DEBUG`
// This is a text heuristic, not data-flow analysis: a guard through a variable (`var dev =
// env.IsDevelopment(); if (dev) ...`) isn't recognised - pass the check inline instead.
public sealed partial class OBS004SensitiveDataLoggingEnabled : IAnalysisRule
{
    private const int MaxLocationsInDescription = 3;

    public string Id => "OBS004";

    public string Title => "Sensitive data logging enabled";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match call in SensitiveDataLoggingCall().Matches(code))
            {
                var argument = SourceFileHeuristics.ReadCallArguments(code, call.Index + call.Length);

                if (!IsLimitedToDevelopment(code, call.Index, argument))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index)));
                }
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
            $"EnableSensitiveDataLogging() is called without a development-only guard ({locations}{more}). It writes "
                + "query parameter values - including personal data - into logs and exception messages. Pass "
                + "builder.Environment.IsDevelopment() as its argument, or wrap the call in an IsDevelopment() check.",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    private static bool IsLimitedToDevelopment(string code, int callIndex, string argument)
    {
        var trimmed = argument.Trim();

        if (trimmed == "false" || DevelopmentGuard.IsDevelopmentCheck(trimmed))
        {
            return true;
        }

        return DevelopmentGuard.Covers(code, callIndex);
    }

    [GeneratedRegex(@"\bEnableSensitiveDataLogging\s*\(")]
    private static partial Regex SensitiveDataLoggingCall();
}
