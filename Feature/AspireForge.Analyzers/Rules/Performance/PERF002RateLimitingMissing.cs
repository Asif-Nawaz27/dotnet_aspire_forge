using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Performance;

public sealed class PERF002RateLimitingMissing : IAnalysisRule
{
    public string Id => "PERF002";

    public string Title => "Rate limiting missing";

    public Severity DefaultSeverity => Severity.Suggestion;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddRateLimiter"))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No call to AddRateLimiter was found. A single client can send unlimited requests, with no built-in protection against abuse or accidental overload.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
