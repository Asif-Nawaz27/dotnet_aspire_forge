using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Performance;

// Unlike the other rules here, this one flags the PRESENCE of a pattern rather than the absence of
// one - the same shape as SEC002's AllowAnyOrigin check. Deliberately narrow: ".GetAwaiter().GetResult()"
// and ".Wait()" are specific enough to rarely false-positive. A bare ".Result" was considered and
// dropped - too many unrelated types (DTOs, wrapper records) have a "Result" property, and a noisy
// rule erodes trust in the ones that aren't.
public sealed partial class PERF003BlockingCallOnAsyncCode : IAnalysisRule
{
    private const int MaxLocationsInDescription = 5;

    public string Id => "PERF003";

    public string Title => "Blocking call on asynchronous code";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match call in BlockingCall().Matches(code))
            {
                findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index)));
            }
        }

        findings.RemoveAll(f => context.Suppressions.IsSuppressed(Id, f.File, f.Line));

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings.OrderBy(f => f.File, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Line).ToList();

        var locations = string.Join(
            ", ",
            findings.Take(MaxLocationsInDescription).Select(f => $"{Path.GetFileName(f.File)}:{f.Line}"));
        var more = findings.Count > MaxLocationsInDescription
            ? $" (and {findings.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"A .GetAwaiter().GetResult() or .Wait() call was found ({locations}{more}). Blocking on async code from a "
                + "synchronous context risks thread-pool starvation and deadlocks under load - await it instead.",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    [GeneratedRegex(@"\.GetAwaiter\(\)\.GetResult\(\)|\.Wait\(\)")]
    private static partial Regex BlockingCall();
}
