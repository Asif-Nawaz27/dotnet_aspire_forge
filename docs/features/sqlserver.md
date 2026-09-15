# SQL Server

```bash
aspireforge add sqlserver
```

Wires up EF Core against SQL Server, following the same
`ASP.NET Core → Application → Infrastructure → EF Core → [provider] → [database]` layering as
[postgres](postgres.md): the database provider is provider-agnostic infrastructure, not something
the Api project talks to directly. See [ARCH001](../rules/architecture.md).

## What it does

| Step | Detail |
|---|---|
| Added SQL Server provider | Adds `Microsoft.EntityFrameworkCore.SqlServer` to `Infrastructure`. |
| Added EF Core configuration | Adds `Microsoft.EntityFrameworkCore.Design` to `Infrastructure` (needed for migrations tooling), then pins `Microsoft.EntityFrameworkCore` to one consistent version across `Infrastructure` **and** `Api` - the provider package and `EFCore.Design` can independently resolve to different EF Core patch versions otherwise, which breaks the build once `Api` references `Infrastructure`. |
| Added DbContext | Writes `src/{Name}.Infrastructure/Persistence/AppDbContext.cs`, a minimal `DbContext` subclass. |
| Added connection configuration | Adds a `SqlServer` entry under `ConnectionStrings` in `Api`'s `appsettings.json`, and wires `builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(...))` into `Api`'s `Program.cs`. |
| Added migrations support | Creates a local `dotnet-ef` tool manifest (if one doesn't already exist) and installs `dotnet-ef` as a local tool, so `dotnet ef migrations add ...` works without a global install. |

## Generated connection string

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database={ProjectName};User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
  }
}
```

This uses SQL authentication (rather than a trusted/Windows connection) so it works against the
Dockerized SQL Server `add docker` generates as well as a local install. It's a local-development
default - replace it (or override it via environment variables/user secrets) before deploying
anywhere real.

## Combining with Docker

If you've also run `aspireforge add docker` (or generated with it, which is the default), re-run
`aspireforge add docker` after `add sqlserver` to refresh `docker-compose.yml` with a matching
`sqlserver` service (`mcr.microsoft.com/mssql/server`). See [`add`](../commands/add.md).
