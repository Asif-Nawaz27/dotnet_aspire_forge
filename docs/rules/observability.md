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

## OBS004 - Sensitive data logging enabled

**Default severity:** `error`

Fires for each call to EF Core's `EnableSensitiveDataLogging()` that isn't limited to development.
The option writes query parameter values - emails, names, tokens, anything a query filters on or
saves - into logs and exception messages. Locally that's useful; in production it copies personal
data into every log sink, which is a privacy breach and often a compliance one (GDPR, PCI).

A call counts as limited to development when:

- its argument is an environment check - `EnableSensitiveDataLogging(builder.Environment.IsDevelopment())`
  or `IsEnvironment("Development")` - or the literal `false`,
- it sits inside an `if (...IsDevelopment()...)` block (braced or braceless, at any nesting depth -
  e.g. inside an `AddDbContext` lambda inside the `if`), or
- it sits inside `#if DEBUG` (before any `#else`/`#endif`).

Negated checks (`!env.IsDevelopment()`), `else` branches, and `#else` sections do **not** count.
This is a text heuristic, not data-flow analysis: a guard through a variable (`var dev =
env.IsDevelopment(); if (dev) ...`) isn't recognised - pass the check inline instead. The finding
reports the file and line of each unguarded call.

**Fix:** `options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment())`.

## OBS005 - Console output instead of ILogger

**Default severity:** `suggestion`

Fires when a web project's code calls `Console.Write`, `Console.WriteLine`, or their
`Console.Error` variants. Console output has no log level, category, structured properties, or trace
correlation, and never reaches the configured log sinks (OpenTelemetry, Serilog, ...). Matches
inside comments are ignored, and non-web projects (CLI tools, workers without
`WebApplication.CreateBuilder`) are skipped since writing to the console is legitimate there. The
finding reports the file and line of each call.

**Fix:** Inject `ILogger<T>` and call `logger.LogInformation(...)`.

## OBS006 - Log message built from values

**Default severity:** `suggestion`

Flags log messages built from values at the call site - `$"User {id} signed in"`,
`"User " + id`, or `string.Format(...)` - instead of a message template with named placeholders:

- **Structure is lost.** With `LogInformation("User {UserId} signed in", userId)` the log sink stores
  `UserId` as a field you can filter and aggregate on. An interpolated message is just a string.
- **It costs even when nothing is logged.** The string is built before the logger checks whether the
  level is enabled, allocating on every call.
- **Grouping breaks.** Every message text is distinct, so log-based alerts and "count by message"
  queries stop working.

This is the same check as code analysis rule
[CA2254](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2254).

Calls checked - the message is the first argument that's a string expression, so
`LogError(ex, $"...")` and `Log(LogLevel.Warning, eventId, $"...")` are covered:

| Library | Methods |
| --- | --- |
| Microsoft.Extensions.Logging | `LogTrace`, `LogDebug`, `LogInformation`, `LogWarning`, `LogError`, `LogCritical`, `Log`, `BeginScope` |
| Serilog (files with `using Serilog;`) | `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| NLog (files with `using NLog;`) | `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal` |

Serilog and NLog method names are only checked in files that import those libraries, so look-alikes
such as `Result.Error($"...")` aren't flagged. Interpolated strings without holes, `{{escaped}}`
braces, constant concatenation (`"a" + "b"`), a message passed in as a variable, and comments are fine.
The finding lists each call with its file and line.

**Fix:** Use a message template with named placeholders, and pass the values as arguments:

```csharp
logger.LogInformation("User {UserId} signed in from {IpAddress}", userId, ip);
```

For hot paths, the `[LoggerMessage]` source generator avoids the remaining overhead entirely.
