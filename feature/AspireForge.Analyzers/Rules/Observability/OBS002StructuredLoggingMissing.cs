using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

public sealed class OBS002StructuredLoggingMissing : IAnalysisRule
{
    private static readonly string[] StructuredLoggingPackagePrefixes =
    [
        "Serilog",
        "NLog",
        "Microsoft.Extensions.Logging.ApplicationInsights",
    ];

    public string Id => "OBS002";

    public string Title => "Structured logging missing";

    public Severity DefaultSeverity => Severity.Info;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var hasStructuredLogging = context.PackageReferences.Any(package =>
            StructuredLoggingPackagePrefixes.Any(
                prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        if (hasStructuredLogging)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        return Task.FromResult<AnalysisIssue?>(new AnalysisIssue(
            Id,
            Title,
            "No structured logging provider (e.g. Serilog, NLog) was found. Log output will be harder to query and correlate.",
            DefaultSeverity,
            context.ProjectFile));
    }
}
