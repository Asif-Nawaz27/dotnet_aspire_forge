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

    // Mirrors ARCH001: a package reference alone doesn't prove the provider is actually wired up -
    // only that it's available. Each provider needs its own registration call to do anything.
    private static readonly string[] StructuredLoggingUsageTokens =
    [
        "UseSerilog(",
        "AddSerilog(",
        "UseNLog(",
        "AddNLog(",
        "AddApplicationInsightsTelemetry(",
    ];

    public string Id => "OBS002";

    public string Title => "Structured logging missing";

    public Severity DefaultSeverity => Severity.Info;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var hasStructuredLoggingPackage = context.PackageReferences.Any(package =>
            StructuredLoggingPackagePrefixes.Any(
                prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "Logging.AddOpenTelemetry("))
        {
            return null;
        }

        if (hasStructuredLoggingPackage
            && await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, StructuredLoggingUsageTokens))
        {
            return null;
        }

        if (hasStructuredLoggingPackage)
        {
            return new AnalysisIssue(
                Id,
                Title,
                "A structured logging package is referenced, but no call to wire it up (UseSerilog, AddNLog, "
                    + "AddApplicationInsightsTelemetry, etc.) was found. The package alone doesn't replace the default logger.",
                DefaultSeverity,
                context.ProjectFile);
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No structured logging provider (e.g. Serilog, NLog, or OpenTelemetry logging) was found. "
                + "Log output will be harder to query and correlate.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
