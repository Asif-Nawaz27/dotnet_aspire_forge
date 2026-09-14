# PostgreSQL

```bash
aspireforge add postgres
```

Wires up EF Core against PostgreSQL, following the
`ASP.NET Core → Application → Infrastructure → EF Core → Npgsql → PostgreSQL` layering: the
database provider is provider-agnostic infrastructure, not something the Api project talks to
directly. See [ARCH001](../rules/architecture.md).

## What it does

| Step | Detail |
|---|---|
| Added PostgreSQL provider | Adds `Npgsql.EntityFrameworkCore.PostgreSQL` to `Infrastructure`. |
| Added EF Core configuration | Adds `Microsoft.EntityFrameworkCore.Design` to `Infrastructure` (needed for migrations tooling), then pins `Microsoft.EntityFrameworkCore` to one consistent version across `Infrastructure` **and** `Api` - the provider package and `EFCore.Design` can independently resolve to different EF Core patch versions otherwise, which breaks the build once `Api` references `Infrastructure`. |
| Added DbContext | Writes `src/{Name}.Infrastructure/Persistence/AppDbContext.cs`, a minimal `DbContext` subclass. |
| Added connection configuration | Adds a `Postgres` entry under `ConnectionStrings` in `Api`'s `appsettings.json`, and wires `builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(...))` into `Api`'s `Program.cs`. |
| Added migrations support | Creates a local `dotnet-ef` tool manifest (if one doesn't already exist) and installs `dotnet-ef` as a local tool, so `dotnet ef migrations add ...` works without a global install. |

## Generated connection string

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database={ProjectName};Username=postgres;Password=postgres"
  }
}
```

This is a local-development default - replace it (or override it via environment variables/user
secrets) before deploying anywhere real.

## Combining with Docker

If you've also run `aspireforge add docker` (or generated with it, which is the default), re-run
`aspireforge add docker` after `add postgres` to refresh `docker-compose.yml` with a matching
`postgres` service. See [`add`](../commands/add.md).
