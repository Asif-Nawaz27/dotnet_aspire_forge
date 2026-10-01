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

## REL003 - Outbound HTTP without resilience

**Default severity:** `warning`

Fires on either of:

- A direct `new HttpClient(` - bypasses `IHttpClientFactory`, risking socket exhaustion and stale DNS.
- `AddHttpClient` with no resilience handler (`AddStandardResilienceHandler`, `AddResilienceHandler`,
  or a Polly policy) anywhere in the project's source. A `ConfigureHttpClientDefaults(http =>
  http.AddStandardResilienceHandler())` in `ServiceDefaults` satisfies it.

**Fix:** Register clients with `AddHttpClient` and add `.AddStandardResilienceHandler()`
(`Microsoft.Extensions.Http.Resilience`).

## REL004 - Health checks don't check dependencies

**Default severity:** `warning`

Fires when the project is a web app that references a database or Redis package
(`Microsoft.EntityFrameworkCore*`, `Npgsql*`, `Microsoft.Data.SqlClient`, `StackExchange.Redis`,
`MongoDB.Driver`) but registers no dependency health check (`AddDbContextCheck`, `AddNpgSql`,
`AddSqlServer`, `AddRedis`, `AddMongoDb`, or a custom `AddCheck`). Readiness would report `Healthy`
while the database is down. Projects using an `Aspire.*` client integration are skipped, since those
register their own checks.

**Fix:** `services.AddHealthChecks().AddDbContextCheck<AppDbContext>("db", tags: ["ready"])`.

## REL005 - EnsureCreated used instead of migrations

**Default severity:** `warning`

Fires on any `EnsureCreated()` / `EnsureCreatedAsync()` call. It creates the schema without
migration history, so later model changes can never be applied.

**Fix:** Use `Database.Migrate()` or apply migrations in your deployment pipeline.

## REL006 - Database retry policy missing

**Default severity:** `suggestion`

Fires when `UseNpgsql` or `UseSqlServer` is used but `EnableRetryOnFailure` never is, so transient
database errors fail requests instead of being retried.

**Fix:** `options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())`.

## REL007 - Settings not validated at startup

**Default severity:** `warning`

Fires when configuration is bound to options without `ValidateOnStart`:

- `services.Configure<T>(configuration.GetSection(...))` - can never be validated on start, so it
  is always flagged.
- `AddOptions<T>().Bind(...)` / `.BindConfiguration(...)` with fewer `ValidateOnStart()` calls than
  bindings.

Bad settings then only fail on first use, mid-request.

**Fix:** `services.AddOptions<T>().BindConfiguration("Section").ValidateDataAnnotations().ValidateOnStart()`.
