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
        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddOpenTelemetry("))
        {
            return new AnalysisIssue(
                Id,
                Title,
                "No call to AddOpenTelemetry was found. Traces, metrics, and distributed context will not be collected.",
                DefaultSeverity,
                context.ProjectFile);
        }

        // AddOpenTelemetry() alone configures nothing - it's just the builder. Without at least one
        // signal wired to it (WithTracing/WithMetrics/WithLogging, typically followed by an exporter),
        // nothing is actually being collected or exported.
        if (!await SourceFileHeuristics.ContainsAnyAsync(
                context, cancellationToken, "WithTracing(", "WithMetrics(", "WithLogging("))
        {
            return new AnalysisIssue(
                Id,
                Title,
                "AddOpenTelemetry was found, but no WithTracing, WithMetrics, or WithLogging call was. The builder is configured but no signal is actually wired to it, so nothing is collected.",
                DefaultSeverity,
                context.ProjectFile);
        }

        return null;
    }
}
