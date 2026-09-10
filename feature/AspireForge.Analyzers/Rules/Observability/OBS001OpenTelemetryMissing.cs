using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

public sealed class OBS001OpenTelemetryMissing : IAnalysisRule
{
    public string Id => "OBS001";

    public string Title => "OpenTelemetry missing";

    public Severity DefaultSeverity => Severity.Info;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var hasOpenTelemetry = context.PackageReferences.Any(
            package => package.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase));

        if (hasOpenTelemetry)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        return Task.FromResult<AnalysisIssue?>(new AnalysisIssue(
            Id,
            Title,
            "No OpenTelemetry package reference was found. Traces, metrics, and distributed context will not be collected.",
            DefaultSeverity,
            context.ProjectFile));
    }
}
