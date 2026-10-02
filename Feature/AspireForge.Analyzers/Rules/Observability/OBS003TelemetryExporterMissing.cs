using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

// OBS001 checks that OpenTelemetry collects something; this checks that it sends it somewhere.
// Without an exporter the SDK still pays to create spans and metrics, then drops them - OBS001 passes
// while dashboards stay empty. An exporter registered conditionally (the Aspire ServiceDefaults
// pattern: UseOtlpExporter() only when OTEL_EXPORTER_OTLP_ENDPOINT is set) counts: the decision is
// left to configuration, which is the point.
public sealed class OBS003TelemetryExporterMissing : IAnalysisRule
{
    private static readonly string[] SignalTokens =
    [
        "WithTracing(",
        "WithMetrics(",
        "WithLogging(",
        "Logging.AddOpenTelemetry(",
    ];

    private static readonly string[] ExporterTokens =
    [
        "UseOtlpExporter(",
        "AddOtlpExporter(",
        "UseAzureMonitor(",
        "UseAzureMonitorExporter(",
        "AddAzureMonitorTraceExporter(",
        "AddAzureMonitorMetricExporter(",
        "AddAzureMonitorLogExporter(",
        "AddApplicationInsightsTelemetry(",
        "AddPrometheusExporter(",
        "AddPrometheusHttpListener(",
        "AddZipkinExporter(",
        "AddJaegerExporter(",
        "UseGrafana(",
    ];

    // Documented by OpenTelemetry as a debugging aid: human-readable, unbatched, written to stdout.
    private const string ConsoleExporterToken = "AddConsoleExporter(";

    public string Id => "OBS003";

    public string Title => "Telemetry exporter missing";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        // Nothing collected means nothing to export - OBS001 reports that case.
        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddOpenTelemetry(")
            || !await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, SignalTokens))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, ExporterTokens))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, ConsoleExporterToken))
        {
            return new AnalysisIssue(
                Id,
                Title,
                "OpenTelemetry only exports to the console (AddConsoleExporter), which is meant for debugging - its output is unbatched and not queryable. Add a production exporter such as UseOtlpExporter() or UseAzureMonitor().",
                DefaultSeverity,
                context.ProjectFile);
        }

        return new AnalysisIssue(
            Id,
            Title,
            "OpenTelemetry collects telemetry but no exporter is registered (UseOtlpExporter, AddOtlpExporter, UseAzureMonitor, Prometheus, etc.), so every span, metric, and log record is dropped. Register an exporter - for example builder.Services.AddOpenTelemetry().UseOtlpExporter(), configured via OTEL_EXPORTER_OTLP_ENDPOINT.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
