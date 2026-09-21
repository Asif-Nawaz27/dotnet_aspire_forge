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

        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddHealthChecks"))
        {
            return new AnalysisIssue(
                Id,
                Title,
                "No call to AddHealthChecks was found. Orchestrators and load balancers cannot verify this service's readiness or liveness.",
                DefaultSeverity,
                context.ProjectFile);
        }

        // Registering health checks without mapping an endpoint for them leaves nothing for an
        // orchestrator to actually call - the exact failure mode this rule exists to catch.
        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "MapHealthChecks"))
        {
            return new AnalysisIssue(
                Id,
                Title,
                "AddHealthChecks was found, but no call to MapHealthChecks was. Health checks are registered but not exposed as an endpoint, so nothing can actually query them.",
                DefaultSeverity,
                context.ProjectFile);
        }

        return null;
    }
}
