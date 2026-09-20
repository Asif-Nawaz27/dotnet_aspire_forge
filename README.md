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
observability, testing, architecture, and performance - in plain text, JSON, or SARIF for CI.

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

Performance
  ⚠ Response compression missing
  ⚠ Rate limiting missing
  ✓ No blocking calls on async code

--------------------------------
Errors:   0
Warnings: 8
Passed:   5
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
| `aspireforge add sqlserver` | The same, backed by SQL Server. |
| `aspireforge add redis` | A StackExchange.Redis-backed distributed cache. |
| `aspireforge add telemetry` | An OpenTelemetry `ServiceDefaults` project (metrics, traces, logs, health checks), following Microsoft's own Aspire convention. |
| `aspireforge add authentication` | JWT Bearer auth/authorization via ASP.NET Core's own middleware. |
| `aspireforge add docker` | (Re)generates Docker assets, aware of whatever databases/caches are already configured. |
| `aspireforge doctor` | Analyzes a project; text, JSON, or SARIF output. |
| `aspireforge fix <ruleId>` | Automatically resolves a single rule finding (e.g. `aspireforge fix REL001`). |
| `aspireforge rules list` | Prints the full rule catalog. |

Full command reference: [`new`](docs/commands/new.md) · [`add`](docs/commands/add.md) ·
[`doctor`](docs/commands/doctor.md) · [`fix`](docs/commands/fix.md). Rules can be tuned per project
via [`.aspireforge/config.json`](docs/architecture/rule-engine.md#configuration).

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
| ARCH001 | Architecture | Api doesn't reference *and use* persistence packages directly | warning |
| PERF001 | Performance | Response compression configured | suggestion |
| PERF002 | Performance | Rate limiting configured | suggestion |
| PERF003 | Performance | No blocking calls (`.Wait()`, `.GetAwaiter().GetResult()`) on async code | warning |

Details and fixes for each: [security](docs/rules/security.md) ·
[reliability](docs/rules/reliability.md) · [observability](docs/rules/observability.md) ·
[testing](docs/rules/testing.md) · [architecture](docs/rules/architecture.md) ·
[performance](docs/rules/performance.md).

## Roadmap

v0.1 shipped the CLI, `new`, `doctor`, Postgres, Docker, health checks, OpenTelemetry, basic
(JWT Bearer) authentication, 10 analysis rules, JSON output, tests, CI, and the NuGet package. v0.2
added Redis, SQL Server, SARIF, and per-project configuration. v0.3 added `fix` and a first
Performance category (`PERF001`-`PERF003`), plus made rule detection smarter where it mattered
most: `ARCH001` now checks that a persistence package is actually *used* in `Api`'s own code, not
just referenced, and every text-based rule ignores matches inside comments. Still ahead, in rough
order of what's next:

- **`aspireforge update`** to pull in newer versions of AspireForge-generated scaffolding (Docker
  assets, CI workflow, etc.) into an existing project.
- **`rules install <pack>`** - third-party rule packages built on the same `IAnalysisRule`
  contract as the built-in rules.
- **A distributable GitHub Action** wrapping `aspireforge doctor`, so other repos can run it in CI
  without hand-writing the workflow steps AspireForge's own [`ci.yml`](.github/workflows/ci.yml) uses.
- **More security, performance, and architecture rules**, and pushing more of them past plain text
  search toward real Roslyn semantic analysis, closer to what a compiler actually sees.
- **More fixes** - most of the 13 rules don't have an automatic fix yet; see
  [`fix`](docs/commands/fix.md#fixable-rules-today) for which do and why the rest don't.
- **MySQL** as another `add` database provider, alongside Postgres and SQL Server.
- **`vertical-slice` architecture** as a second `--architecture` option alongside `clean`.
- **ASP.NET Core Identity, Entra ID, and Keycloak** as `add identity`/`add entra`/`add keycloak`,
  building on the JWT Bearer foundation `add authentication` already provides.
- **A .NET Aspire AppHost option** for `new`, as an alternative to the generated
  `docker-compose.yml` for local orchestration.
- **Better generated templates** - richer starting content in the generated Domain/Application
  layers than today's empty scaffolding.
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
