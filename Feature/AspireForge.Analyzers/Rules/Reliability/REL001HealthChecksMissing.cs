using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

public sealed class REL001HealthChecksMissing : IAnalysisRule
{
    public string Id => "REL001";

    public string Title => "Health checks missing";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddHealthChecks"))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No call to AddHealthChecks was found. Orchestrators and load balancers cannot verify this service's readiness or liveness.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
