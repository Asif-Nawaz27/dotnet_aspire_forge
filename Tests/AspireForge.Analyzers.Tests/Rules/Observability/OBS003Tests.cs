using AspireForge.Analyzers.Rules.Observability;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Observability;

public class OBS003Tests
{
    private readonly OBS003TelemetryExporterMissing _rule = new();

    [Fact]
    public async Task Fires_WhenTelemetryIsCollectedButNeverExported()
    {
        using var project = TestProject.Create("""
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
                .WithMetrics(metrics => metrics.AddRuntimeInstrumentation());
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("OBS003", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Contains("no exporter is registered", issue.Description);
    }

    [Fact]
    public async Task Fires_ForOpenTelemetryLoggingWithoutAnExporter()
    {
        using var project = TestProject.Create("builder.Logging.AddOpenTelemetry(logging => logging.IncludeScopes = true);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Fact]
    public async Task Fires_WithASpecificMessage_WhenOnlyTheConsoleExporterIsRegistered()
    {
        using var project = TestProject.Create(
            "builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddConsoleExporter());");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Contains("only exports to the console", issue!.Description);
    }

    [Theory]
    [InlineData("builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource(\"App\")).UseOtlpExporter();")]
    [InlineData("builder.Services.AddOpenTelemetry().WithTracing(t => t.AddOtlpExporter());")]
    [InlineData("builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(\"App\")).UseAzureMonitor();")]
    [InlineData("builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddPrometheusExporter());")]
    [InlineData("builder.Services.AddOpenTelemetry().WithTracing(t => t.AddZipkinExporter());")]
    [InlineData("builder.Services.AddOpenTelemetry().WithTracing(t => t.AddConsoleExporter().AddOtlpExporter());")]
    public async Task DoesNotFire_WhenAProductionExporterIsRegistered(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    // The Aspire ServiceDefaults pattern: export only when an endpoint is configured.
    [Fact]
    public async Task DoesNotFire_ForAConditionallyRegisteredExporter()
    {
        using var project = TestProject.Create("""
            builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddAspNetCoreInstrumentation());

            if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("var builder = WebApplication.CreateBuilder(args);")]
    [InlineData("builder.Services.AddOpenTelemetry();")]
    public async Task DoesNotFire_WhenNothingIsCollected_BecauseOBS001CoversIt(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task Fires_WhenTheOnlyExporterIsCommentedOut()
    {
        using var project = TestProject.Create("""
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation());
            // .UseOtlpExporter();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }
}
