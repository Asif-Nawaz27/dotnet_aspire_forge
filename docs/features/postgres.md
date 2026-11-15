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
| Added DbContext | Writes `src/{Name}.Infrastructure/Persistence/AppDbContext.cs`, a minimal `DbContext` subclass, and `PersistenceServiceCollectionExtensions.cs` with an `AddPersistence(connectionString)` method that registers it with `UseNpgsql(..., database => database.EnableRetryOnFailure())`, so transient errors are retried ([REL006](../rules/reliability.md)). |
| Added connection configuration | Adds an empty `Postgres` entry under `ConnectionStrings` in `Api`'s `appsettings.json` and the local connection string to `appsettings.Development.json`, and adds `builder.Services.AddPersistence(builder.Configuration.GetConnectionString("Postgres"));` to `Api`'s `Program.cs`. The Api never references EF Core types itself ([ARCH001](../rules/architecture.md)). |
| Added migrations support | Creates a local `dotnet-ef` tool manifest (if one doesn't already exist) and installs `dotnet-ef` as a local tool, so `dotnet ef migrations add ...` works without a global install. |

## Connection strings

`appsettings.json` ships inside every publish output and container image, so it only gets an empty
entry ([SEC004](../rules/security.md#sec004---secrets-in-configuration-files)). The local
connection string goes into `appsettings.Development.json`, so `dotnet run` works out of the box:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database={ProjectName};Username=postgres;Password=postgres"
  }
}
```

Every other environment supplies its own, as the environment variable `ConnectionStrings__Postgres`
or from a secret store such as Azure Key Vault.

## Combining with Docker

If you've also run `aspireforge add docker` (or generated with it, which is the default), re-run
`aspireforge add docker` after `add postgres` to refresh `docker-compose.yml` with a matching
`postgres` service.
The api container gets `ConnectionStrings__Postgres` from the compose file, pointing at that
service. See [`add`](../commands/add.md).
