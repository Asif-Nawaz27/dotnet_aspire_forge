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
| Added DbContext | Writes `src/{Name}.Infrastructure/Persistence/AppDbContext.cs`, a minimal `DbContext` subclass, and `PersistenceServiceCollectionExtensions.cs` with an `AddPersistence(connectionString)` method that registers it with `UseSqlServer(..., database => database.EnableRetryOnFailure())`, so transient errors are retried ([REL006](../rules/reliability.md)). |
| Added connection configuration | Adds an empty `SqlServer` entry under `ConnectionStrings` in `Api`'s `appsettings.json` and the local connection string to `appsettings.Development.json`, and adds `builder.Services.AddPersistence(builder.Configuration.GetConnectionString("SqlServer"));` to `Api`'s `Program.cs`. The Api never references EF Core types itself ([ARCH001](../rules/architecture.md)). |
| Added migrations support | Creates a local `dotnet-ef` tool manifest (if one doesn't already exist) and installs `dotnet-ef` as a local tool, so `dotnet ef migrations add ...` works without a global install. |

## Connection strings

`appsettings.json` ships inside every publish output and container image, so it only gets an empty
entry ([SEC004](../rules/security.md#sec004---secrets-in-configuration-files)). The local
connection string goes into `appsettings.Development.json`, so `dotnet run` works out of the box:

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database={ProjectName};User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
  }
}
```

It uses SQL authentication (rather than a trusted/Windows connection) so it works against the
Dockerized SQL Server as well as a local install. `TrustServerCertificate=True` accepts the
container's self-signed certificate, which is why it stays in the development file
([SEC010](../rules/security.md#sec010---tls-certificate-validation-disabled)).

Every other environment supplies its own, as the environment variable `ConnectionStrings__SqlServer`
or from a secret store such as Azure Key Vault.

## Combining with Docker

If you've also run `aspireforge add docker` (or generated with it, which is the default), re-run
`aspireforge add docker` after `add sqlserver` to refresh `docker-compose.yml` with a matching
`sqlserver` service (`mcr.microsoft.com/mssql/server`).
The api container gets `ConnectionStrings__SqlServer` from the compose file, pointing at that
service. See [`add`](../commands/add.md).
