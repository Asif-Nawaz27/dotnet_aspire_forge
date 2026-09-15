# AspireForge

**Production-readiness tooling for ASP.NET Core APIs.**

Create. Configure. Audit. Ship.

```bash
dotnet tool install --global AspireForge

aspireforge new Orders.Api

cd Orders.Api

aspireforge doctor
```

---

## What it is

AspireForge is a .NET global tool with three jobs: **generate** a clean-architecture ASP.NET Core
solution, **add** production concerns to it (a database, caching, telemetry, auth, Docker) on
demand, and **audit** any project against a rule set covering security, reliability,
observability, testing, and architecture - in plain text, JSON, or SARIF for CI.

## Why it exists

Most APIs don't ship with authentication, health checks, structured logging, or CORS locked down
because nobody set them up on day one, not because the team doesn't care. `dotnet new webapi` gets
you a project; it doesn't tell you what's missing before it becomes an incident. AspireForge is the
part of the workflow that checks - at generation time, whenever you add something, and in CI on
every pull request.

## Installation

```bash
dotnet tool install --global AspireForge
```

Requires the .NET 10 SDK. See [Installation](docs/getting-started/installation.md) for upgrading,
uninstalling, and verifying the install.

## Quick start

```bash
aspireforge new Orders.Api
cd Orders.Api
aspireforge doctor
aspireforge add postgres
aspireforge add telemetry
dotnet build
dotnet test
```

See [Your first API](docs/getting-started/first-api.md) for a walkthrough, or
[Using an existing API](docs/getting-started/existing-api.md) if you didn't generate the project
with AspireForge.

## `doctor` example

```
AspireForge Doctor

Project: Orders.Api.Api
Framework: net10.0

Security
  ⚠ Authentication not configured
  ✓ CORS policy restricts origins
  ✓ HTTPS configured

Reliability
  ⚠ Health checks missing
  ⚠ Global exception handling missing

Observability
  ⚠ OpenTelemetry missing
  ⚠ Structured logging missing

Testing
  ⚠ No test project detected
  ✓ Integration tests detected

Architecture
  ✓ No obvious dependency violations

--------------------------------
Errors:   0
Warnings: 6
Passed:   4
--------------------------------

Production Readiness: 4/10
```

`--format json` and `--format sarif` produce the same findings for tooling and GitHub code
scanning. `--fail-on warning` makes CI fail on warnings, not just errors. Details:
[`doctor`](docs/commands/doctor.md).

## Features

| Command | What it does |
|---|---|
| `aspireforge new <name>` | Generates a clean-architecture solution (Domain/Application/Infrastructure/Api), plus a Dockerfile, docker-compose.yml, and a GitHub Actions CI workflow. |
| `aspireforge add postgres` | Npgsql, an EF Core `DbContext`, connection config, and migrations tooling. |
| `aspireforge add redis` | A StackExchange.Redis-backed distributed cache. |
| `aspireforge add telemetry` | An OpenTelemetry `ServiceDefaults` project (metrics, traces, logs, health checks), following Microsoft's own Aspire convention. |
| `aspireforge add authentication` | JWT Bearer auth/authorization via ASP.NET Core's own middleware. |
| `aspireforge add docker` | (Re)generates Docker assets, aware of whatever databases/caches are already configured. |
| `aspireforge doctor` | Analyzes a project; text, JSON, or SARIF output. |
| `aspireforge rules list` | Prints the full rule catalog. |

Full command reference: [`new`](docs/commands/new.md) · [`add`](docs/commands/add.md) ·
[`doctor`](docs/commands/doctor.md). Rules can be tuned per project via
[`.aspireforge/config.json`](docs/architecture/rule-engine.md#configuration).

## Supported rules

| ID | Category | Checks for | Default severity |
|---|---|---|---|
| SEC001 | Security | Authentication configured | warning |
| SEC002 | Security | CORS doesn't allow any origin | error |
| SEC003 | Security | HTTPS redirection configured | warning |
| REL001 | Reliability | Health checks configured | warning |
| REL002 | Reliability | Global exception handling configured | warning |
| OBS001 | Observability | OpenTelemetry configured | info |
| OBS002 | Observability | Structured logging configured | info |
| TEST001 | Testing | A matching unit test project exists | warning |
| TEST002 | Testing | An integration test project exists | info |
| ARCH001 | Architecture | Api doesn't reference persistence packages directly | warning |

Details and fixes for each: [security](docs/rules/security.md) ·
[reliability](docs/rules/reliability.md) · [observability](docs/rules/observability.md) ·
[testing](docs/rules/testing.md) · [architecture](docs/rules/architecture.md).

## Roadmap

Deliberately not in v0.1, in rough order of what's next:

- **`vertical-slice` architecture** as a second `--architecture` option alongside `clean`.
- **SQL Server and MySQL** as additional `add` database providers, alongside Postgres.
- **ASP.NET Core Identity, Entra ID, and Keycloak** as `add identity`/`add entra`/`add keycloak`,
  building on the JWT Bearer foundation `add authentication` already provides.
- **`rules install <pack>`** - third-party rule packages built on the same `IAnalysisRule`
  contract as the built-in rules.
- **A .NET Aspire AppHost option** for `new`, as an alternative to the generated
  `docker-compose.yml` for local orchestration.
- **`doctor --config <path>`** to point at a config file outside the default
  `.aspireforge/` discovery.

See [Architecture overview](docs/architecture/overview.md) for how today's design already leaves
room for each of these.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for the project layout and how
to build/test locally, and [SECURITY.md](SECURITY.md) to report a vulnerability privately. Please
open an issue before starting significant work:
[github.com/Asif-Nawaz27/dotnet_aspire_forge/issues](https://github.com/Asif-Nawaz27/dotnet_aspire_forge/issues).

## License

[MIT](LICENSE)

## Sponsorship

AspireForge doesn't have a funding mechanism set up yet. If that changes, it'll be linked here -
for now, opening issues, sending pull requests, and spreading the word are the most useful ways to
support the project.
