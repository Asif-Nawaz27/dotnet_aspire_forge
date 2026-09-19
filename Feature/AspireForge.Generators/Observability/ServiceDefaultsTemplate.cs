namespace AspireForge.Generators.Observability;

internal static class ServiceDefaultsTemplate
{
    public static string Extensions(string projectName) => $$"""
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Diagnostics.HealthChecks;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Diagnostics.HealthChecks;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;
        using OpenTelemetry;
        using OpenTelemetry.Logs;
        using OpenTelemetry.Metrics;
        using OpenTelemetry.Resources;
        using OpenTelemetry.Trace;

        namespace {{projectName}}.ServiceDefaults;

        public static class Extensions
        {
            public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
                where TBuilder : IHostApplicationBuilder
            {
                builder.ConfigureOpenTelemetry();
                builder.AddDefaultHealthChecks();

                return builder;
            }

            public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
                where TBuilder : IHostApplicationBuilder
            {
                builder.Logging.AddOpenTelemetry(logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;
                });

                builder.Services.AddOpenTelemetry()
                    .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
                    .WithMetrics(metrics => metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation())
                    .WithTracing(tracing => tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation());

                var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    builder.Services.AddOpenTelemetry().UseOtlpExporter();
                }

                return builder;
            }

            public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
                where TBuilder : IHostApplicationBuilder
            {
                builder.Services.AddHealthChecks()
                    .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

                return builder;
            }

            public static WebApplication MapDefaultEndpoints(this WebApplication app)
            {
                app.MapHealthChecks("/health");

                app.MapHealthChecks("/alive", new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("live"),
                });

                return app;
            }
        }
        """;
}
