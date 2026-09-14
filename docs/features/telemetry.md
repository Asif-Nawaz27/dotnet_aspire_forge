# Telemetry

```bash
aspireforge add telemetry
```

Adds observability by following Microsoft's own [.NET Aspire ServiceDefaults
convention](https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults) rather than
wiring OpenTelemetry directly into `Api`'s `Program.cs`: a separate, referenceable project that
configures metrics, traces, logs, and health checks in one place - the same shape any Aspire-aware
service is expected to have, so this integrates with the wider Aspire ecosystem instead of
inventing its own approach.

## What it does

| Step | Detail |
|---|---|
| Added ServiceDefaults project | Creates `src/{Name}.ServiceDefaults`, a class library, and adds a `Microsoft.AspNetCore.App` framework reference (needed for `WebApplication`/`MapHealthChecks`, which a plain class library doesn't get by default). |
| Configured metrics and traces | Adds `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`, and `OpenTelemetry.Exporter.OpenTelemetryProtocol`. |
| Configured structured logging | OpenTelemetry logging is configured as part of the same `Extensions.cs`. |
| Configured health checks | `MapHealthChecks`/`AddHealthChecks` come transitively from the `Microsoft.AspNetCore.App` framework reference - no separate package needed. |
| Wired ServiceDefaults into the Api | Adds a project reference from `Api` to `ServiceDefaults`, then inserts `builder.AddServiceDefaults();` (service registration) and `app.MapDefaultEndpoints();` (health endpoints) into `Api`'s `Program.cs`. |

## Verifying it

```bash
dotnet run --project src/{Name}.Api
curl http://localhost:<port>/health
curl http://localhost:<port>/alive
```

## Exporting

By default, traces/metrics/logs are exported via OTLP (`OpenTelemetry.Exporter.OpenTelemetryProtocol`)
to whatever endpoint is configured (for example, an [Aspire
dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview) or a
collector) via the standard `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable.
