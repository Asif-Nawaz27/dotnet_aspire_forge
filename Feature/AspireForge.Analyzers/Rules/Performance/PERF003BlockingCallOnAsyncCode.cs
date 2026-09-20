using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Performance;

// Unlike the other rules here, this one flags the PRESENCE of a pattern rather than the absence of
// one - the same shape as SEC002's AllowAnyOrigin check. Deliberately narrow: ".GetAwaiter().GetResult()"
// and ".Wait()" are specific enough to rarely false-positive. A bare ".Result" was considered and
// dropped - too many unrelated types (DTOs, wrapper records) have a "Result" property, and a noisy
// rule erodes trust in the ones that aren't.
public sealed class PERF003BlockingCallOnAsyncCode : IAnalysisRule
{
    private static readonly string[] BlockingTokens = [".GetAwaiter().GetResult()", ".Wait()"];

    public string Id => "PERF003";

    public string Title => "Blocking call on asynchronous code";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, BlockingTokens))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "A .GetAwaiter().GetResult() or .Wait() call was found. Blocking on async code from a synchronous "
                + "context risks thread-pool starvation and deadlocks under load - await it instead.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
