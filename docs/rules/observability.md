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
