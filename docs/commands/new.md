# `aspireforge new`

Generates a new project.

## Usage

```bash
aspireforge new <name> [--architecture <name>]
```

| Argument/Option | Description | Default |
|---|---|---|
| `<name>` | The project name. Also used as the solution name and as a prefix for every generated project (`{name}.Api`, `{name}.Infrastructure`, ...). | *(required)* |
| `--architecture` | The architecture template to use. | `clean` |

## Architectures

Only `clean` is implemented today. Passing anything else fails with exit code `2`
(invalid configuration) and lists the supported values.

`clean` composes existing .NET templates (`dotnet new classlib`/`webapi`/`xunit`) rather than a
bespoke templating engine - see [Architecture overview](../architecture/overview.md). It produces:

```
<name>/
├── src/
│   ├── <name>.Domain/
│   ├── <name>.Application/
│   ├── <name>.Infrastructure/
│   ├── <name>.Api/
│   └── <name>.ServiceDefaults/
├── tests/
│   └── <name>.Api.IntegrationTests/
├── Dockerfile
├── .dockerignore
├── docker-compose.yml
├── .gitignore
├── README.md
├── <name>.slnx
└── .github/workflows/ci.yml
```

`new` needs network access: it adds NuGet packages (OpenTelemetry, HTTP resilience, and the latest
`Microsoft.AspNetCore.OpenApi`) while generating.

## Production-ready out of the box

A generated project passes AspireForge's own [`doctor`](doctor.md) - every rule except one:

```
⚠ Authentication not configured                                 SEC001
Production Readiness: 9/10 (95/100)
```

Authentication is left to you on purpose: which scheme to use (JWT, cookies, Entra ID, ...) is a real
decision. `aspireforge add authentication` adds JWT bearer, after which the project scores `10/10`.
A test in AspireForge's own suite (`GeneratedProjectDoctorTests`) generates a project and runs every
rule against it, so the generator and the rules can't drift apart.

What makes it pass:

| Part | What `new` puts there | Rules it satisfies |
| --- | --- | --- |
| `<name>.Api/Program.cs` | `AddProblemDetails` + `UseExceptionHandler` (first in the pipeline), `UseHsts` outside development, `UseHttpsRedirection`, response compression, a per-client rate limit (100 requests/minute), OpenAPI in development only, and a `GET /` status endpoint - in the middleware order SEC009 expects | REL002, SEC003, SEC006, SEC007, SEC009, PERF001, PERF002 |
| `<name>.ServiceDefaults` | the [`add telemetry`](add.md) project - OpenTelemetry traces, metrics, and logs with an OTLP exporter, `/health` and `/alive`, and HTTP resilience for every `HttpClient` | REL001, REL003, OBS001, OBS002, OBS003 |
| `Dockerfile` | multi-stage build, pinned base images, `USER $APP_UID` (non-root), with a `.dockerignore` | DEP003 |
| `<name>.Api.IntegrationTests` | an xUnit project named after the Api it tests | TEST001, TEST002 |
| `Microsoft.AspNetCore.OpenApi` | updated to the latest stable version - the version the SDK's `webapi` template pins (10.0.9) depends on a `Microsoft.OpenApi` release with a known high-severity vulnerability | DEP002 |

The rate limit partitions by client IP address. Behind a reverse proxy, configure forwarded headers
so it sees the real client rather than the proxy - or adjust or remove the limiter in `Program.cs`.

## CI workflow

`.github/workflows/ci.yml` restores, builds, and tests the solution on pushes and pull requests to
`main`, then runs [`doctor`](doctor.md) on `<name>.Api`:

| Step | What it does | Can it fail the build? |
| --- | --- | --- |
| Install AspireForge | Installs the exact version that generated the project, so CI's rules only change when you change that line. | Yes |
| Doctor summary | Writes the [Markdown report](doctor.md#markdown-output) to the job summary page. | No |
| Doctor SARIF, Upload to code scanning | Shows findings as code scanning alerts. Needs GitHub Advanced Security on a private repository; without it the upload fails and the build carries on. | No |
| Doctor | The check itself, with the full report in the job log. Fails on `error` and `critical` findings. | Yes |

A new project passes: its one finding, SEC001, is a warning. To be stricter, add `--fail-on warning`
to the last step. If that fails on findings you can't fix yet, accept them with a
[baseline](doctor.md#adopting-doctor-on-an-existing-project), which goes in `src/<name>.Api/.aspireforge/`,
and CI will fail on new findings only.

## Example

```bash
aspireforge new OrdersApi --architecture clean
```

```
Created OrdersApi (clean architecture):
  OrdersApi.slnx
  src/OrdersApi.Domain/
  src/OrdersApi.Application/
  src/OrdersApi.Infrastructure/
  src/OrdersApi.Api/
  tests/OrdersApi.Api.IntegrationTests/
  Added README.md
  Added .gitignore
  Added ServiceDefaults project
  Configured metrics and traces
  Configured structured logging
  Configured health checks
  Wired ServiceDefaults into the Api
  Added Dockerfile
  Added .dockerignore
  Added docker-compose.yml
  Added CI workflow
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Generated successfully. |
| `2` | Invalid project name, or an unsupported `--architecture` value. |
| `3` | Generation started but failed (for example, the target directory already exists). |

See [Exit codes](../architecture/overview.md#exit-codes) for the full scheme shared by every command.
