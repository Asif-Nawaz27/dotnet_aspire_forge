# `aspireforge add`

Adds a feature to an existing AspireForge project. Run it from the project root (the directory
containing the `.sln`/`.slnx` file).

## Usage

```bash
aspireforge add <feature>
```

`add` is a container command - run `aspireforge add --help` (or just `aspireforge add`) to see the
full list, since it's generated from the same registry the CLI dispatches through, so it can never
drift out of date with what's actually implemented.

## Available features

| Feature | What it does |
|---|---|
| `postgres` | Adds Npgsql, an EF Core `DbContext`, connection configuration, and migrations support. See [postgres](../features/postgres.md). |
| `sqlserver` | Adds `Microsoft.EntityFrameworkCore.SqlServer`, an EF Core `DbContext`, connection configuration, and migrations support. See [sqlserver](../features/sqlserver.md). |
| `redis` | Adds the StackExchange.Redis distributed cache client. See [redis](../features/redis.md). |
| `telemetry` | Adds a `ServiceDefaults` project configuring OpenTelemetry metrics/traces/logs and health checks. See [telemetry](../features/telemetry.md). |
| `docker` | (Re)generates `Dockerfile`, `.dockerignore`, and `docker-compose.yml`, wired to whatever databases/caches are already configured. Safe to re-run after adding `postgres`/`sqlserver`/`redis` to refresh the compose file. |
| `authentication` | Adds JWT Bearer authentication/authorization using ASP.NET Core's own middleware, plus an example `/secure` endpoint. |

Each one prints a checklist of what it did:

```
$ aspireforge add postgres
Feature: PostgreSQL

  ✓ Added PostgreSQL provider
  ✓ Added EF Core configuration
  ✓ Added DbContext
  ✓ Added connection configuration
  ✓ Added migrations support

PostgreSQL integration added successfully.
```

## Requirements

`add` locates your `Infrastructure` and `Api` projects by the naming convention `aspireforge new`
produces (`src/{SolutionName}.Infrastructure`, `src/{SolutionName}.Api`). See
[Using AspireForge on an existing API](../getting-started/existing-api.md) if your project doesn't
follow that layout.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Feature added successfully. |
| `2` | No solution file found in the current directory, or an unknown feature name. |
| `3` | The feature installer started but failed partway through. |
