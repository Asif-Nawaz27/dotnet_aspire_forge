using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

public sealed class OBS001OpenTelemetryMissing : IAnalysisRule
{
    public string Id => "OBS001";

    public string Title => "OpenTelemetry missing";

    public Severity DefaultSeverity => Severity.Info;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddOpenTelemetry("))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No call to AddOpenTelemetry was found. Traces, metrics, and distributed context will not be collected.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
