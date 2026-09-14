# Observability rules

## OBS001 - OpenTelemetry missing

**Default severity:** `info`

Fires when no call to `AddOpenTelemetry(` was found anywhere in the project's source (including a
referenced `ServiceDefaults` project - see [`ProjectContext`](../architecture/rule-engine.md)).
Without it, traces, metrics, and distributed context aren't collected.

**Fix:** `aspireforge add telemetry` (see [telemetry](../features/telemetry.md)).

## OBS002 - Structured logging missing

**Default severity:** `info`

Fires unless a structured logging package (`Serilog`, `NLog`, or
`Microsoft.Extensions.Logging.ApplicationInsights`) is referenced, or the source configures
OpenTelemetry logging (`Logging.AddOpenTelemetry(`).

**Fix:** `aspireforge add telemetry` configures OpenTelemetry-based structured logging, or add
Serilog/NLog directly.
