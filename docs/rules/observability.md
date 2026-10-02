# Observability rules

## OBS001 - OpenTelemetry missing

**Default severity:** `info`

Fires when either of two things is missing anywhere in the project's source (including a
referenced `ServiceDefaults` project - see [`ProjectContext`](../architecture/rule-engine.md)):

- No call to `AddOpenTelemetry(` - the builder was never created, or
- `AddOpenTelemetry(` is present but no `WithTracing(`, `WithMetrics(`, or `WithLogging(` call was
  found - the builder exists but no signal is actually wired to it, so nothing is collected.

**Fix:** `aspireforge add telemetry` (see [telemetry](../features/telemetry.md)).

## OBS002 - Structured logging missing

**Default severity:** `info`

Passes if OpenTelemetry logging is configured (`Logging.AddOpenTelemetry(`), or a structured
logging package (`Serilog`, `NLog`, or `Microsoft.Extensions.Logging.ApplicationInsights`) is both
referenced **and** actually wired up (`UseSerilog(`, `AddSerilog(`, `UseNLog(`, `AddNLog(`, or
`AddApplicationInsightsTelemetry(`). Referencing the package alone isn't enough - it doesn't
replace the default logger until one of those calls runs.

**Fix:** `aspireforge add telemetry` configures OpenTelemetry-based structured logging, or add
Serilog/NLog directly and call its setup method.

## OBS003 - Telemetry exporter missing

**Default severity:** `warning`

OBS001 checks that OpenTelemetry collects something; OBS003 checks that it sends it somewhere.
Fires when OpenTelemetry is collecting a signal (`WithTracing(`, `WithMetrics(`, `WithLogging(`, or
`Logging.AddOpenTelemetry(`) but no exporter is registered anywhere in the project's source. The
SDK still pays to create every span and metric, then drops them - so OBS001 passes while dashboards
stay empty.

Recognised exporters: `UseOtlpExporter`, `AddOtlpExporter`, Azure Monitor (`UseAzureMonitor`,
`UseAzureMonitorExporter`, `AddAzureMonitor*Exporter`, `AddApplicationInsightsTelemetry`),
Prometheus (`AddPrometheusExporter`, `AddPrometheusHttpListener`), `AddZipkinExporter`,
`AddJaegerExporter`, and Grafana's `UseGrafana`. An exporter registered conditionally - the Aspire
`ServiceDefaults` pattern of calling `UseOtlpExporter()` only when `OTEL_EXPORTER_OTLP_ENDPOINT` is
set - counts.

`AddConsoleExporter` alone does **not** count: OpenTelemetry documents it as a debugging aid
(unbatched, human-readable stdout), so a console-only setup gets its own message.

Stays silent when nothing is collected at all - that's OBS001's finding, not a second one.

**Fix:** Register an exporter, for example `builder.Services.AddOpenTelemetry().UseOtlpExporter()`
and point `OTEL_EXPORTER_OTLP_ENDPOINT` at your collector. `aspireforge add telemetry` generates
this.
