# Reliability rules

## REL001 - Health checks missing

**Default severity:** `warning`

Fires when the project is a web app but no call to `AddHealthChecks` was found. Without a health
endpoint, orchestrators and load balancers can't verify the service's readiness or liveness.

**Fix:** `aspireforge add telemetry` wires up health checks as part of `ServiceDefaults` (see
[telemetry](../features/telemetry.md)), or call `builder.Services.AddHealthChecks()` directly.

## REL002 - Global exception handling missing

**Default severity:** `warning`

Fires when the project is a web app but neither `UseExceptionHandler` nor `AddExceptionHandler` was
found. Unhandled exceptions will produce default, unformatted error responses.

**Fix:** Register a global exception handler, for example via `app.UseExceptionHandler(...)` or a
custom `IExceptionHandler`.
