# Reliability rules

## REL001 - Health checks missing

**Default severity:** `warning`

Fires when the project is a web app and either of two things is missing:

- No call to `AddHealthChecks` - health checks were never registered at all, or
- `AddHealthChecks` is present but no call to `MapHealthChecks` was found - health checks are
  registered in DI but never exposed as an endpoint, so nothing can actually query them.

**Fix:** `aspireforge add telemetry` wires up both as part of `ServiceDefaults` (see
[telemetry](../features/telemetry.md)), or call `builder.Services.AddHealthChecks()` and
`app.MapHealthChecks("/health")` directly.

## REL002 - Global exception handling missing

**Default severity:** `warning`

Fires when the project is a web app but neither `UseExceptionHandler` nor `AddExceptionHandler` was
found. Unhandled exceptions will produce default, unformatted error responses.

**Fix:** Register a global exception handler, for example via `app.UseExceptionHandler(...)` or a
custom `IExceptionHandler`.
