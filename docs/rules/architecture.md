# Architecture rules

## Architecture styles

The architecture rules check that dependencies point the way your architecture allows, so `doctor`
needs to know which architecture that is. Two styles are supported:

| Style | Layers, dependencies pointing this way | Typical names |
| --- | --- | --- |
| `clean` | Presentation -> Infrastructure -> Application -> Domain | `.Api`, `.Infrastructure`, `.Application`, `.Domain` - what `aspireforge new` generates |
| `layered` (N-tier) | Presentation -> Business -> Data Access -> Entities | `.Web`, `.BLL`, `.DAL`, `.Entities` |

Each project's layer comes from the last segment of its name, ignoring case:

| Style | Layer | Project name ends in |
| --- | --- | --- |
| clean | Domain | `.Domain` |
| clean | Application | `.Application` |
| clean | Infrastructure | `.Infrastructure`, `.Persistence` |
| clean | Presentation | `.Api`, `.WebApi`, `.Web` |
| layered | Entities | `.Entities`, `.Models`, `.Domain` |
| layered | Data Access | `.DataAccess`, `.Data`, `.DAL`, `.Repository`, `.Repositories`, `.Persistence` |
| layered | Business | `.Business`, `.BusinessLogic`, `.BLL`, `.Logic`, `.Services` |
| layered | Presentation | `.Api`, `.WebApi`, `.Web`, `.UI`, `.Presentation`, `.Mvc` |

**Detection.** Without configuration, `doctor` looks at the names of the analysed project and every
project it references, and picks the style more of them belong to. Only names specific to one style
count: `Application`, `Infrastructure` and `Domain` for clean; `Business`, `BusinessLogic`, `BLL`,
`Logic`, `DataAccess`, `Data`, `DAL`, `Repository`, `Repositories` and `Entities` for layered.
`Api`, `Services` and `Models` don't count, because projects of every style use them.

**Setting it.** If detection picks the wrong style, or none, set it in `.aspireforge/config.json`:

```json
{ "architecture": "layered" }
```

The report always says which it used:

```
Architecture: clean (detected)
```

**When it isn't recognised** - a single-project API, or names that tie or match neither style -
ARCH002 and ARCH003 are skipped rather than guessed at. Their checklist lines say `skipped`, not
`✓`, and they don't count as passed. Skipping costs nothing in the score: nothing was found. ARCH001
still runs, because it looks at the API's own code rather than at layers.

## ARCH001 - API directly accesses persistence layer

**Default severity:** `warning`

Fires when the project is a web app that both references a persistence package
(`Microsoft.EntityFrameworkCore*`, `Dapper`, `Npgsql*`, `Microsoft.Data.SqlClient`,
`System.Data.SqlClient`, or `MongoDB.Driver`) **and** its own source actually uses one of that
package's types (`DbContext`, `IDbConnection`, `SqlConnection`, `NpgsqlConnection`, `MongoClient`,
`IMongoCollection`). In the clean architecture layout `aspireforge new` generates, persistence
access should live behind `Infrastructure`, not be referenced directly from `Api`.

Requiring actual usage - not just the package reference - matters because `aspireforge add
postgres` adds a direct `Microsoft.EntityFrameworkCore` reference to `Api` as well as
`Infrastructure`, purely to keep both projects resolving the same EF Core version (see
[postgres](../features/postgres.md)). A package reference with no real usage in `Api`'s own code
(this version-pin case, or a leftover reference after a refactor) no longer trips this rule - only
`Api` actually calling into EF Core, Dapper, or similar does.

### Without a package reference

The more common violation needs no package reference at all. In the clean-architecture layout, `Api`
references `Infrastructure`, so EF Core and the `DbContext` reach it **through that project
reference** - and a controller can inject `AppDbContext` directly. ARCH001 also fires when the Api
project's own code does any of:

- declares a `...DbContext` constructor parameter, field, property, or endpoint lambda parameter
  (`app.MapGet("/orders", (AppDbContext db) => ...)`)
- uses `IDbContextFactory<...>` or `DbSet<...>`
- resolves a `...DbContext` with `GetRequiredService` / `GetService` outside `Program.cs`/`Startup.cs`
- defines a class deriving from `DbContext` (including `IdentityDbContext<...>`)

The composition root is allowed: `AddDbContext<...>()` and other registrations, and resolving the
context in `Program.cs`/`Startup.cs` for startup work such as applying migrations. Only the Api
project's own files are checked - the `DbContext` used inside `Infrastructure` is exactly where it
belongs. `DbContextOptions<...>`, `typeof(...)`, comments, and strings aren't flagged. The finding
lists each usage with its file and line.

**Fix:** Move the persistence package reference to your `Infrastructure` project and access it
through an abstraction (a repository interface, for example) instead of referencing it from `Api`.
For the project-reference case, inject an Application service (or repository interface) into the
controller instead of the `DbContext`, and keep the `DbContext` inside `Infrastructure`.

## ARCH002 - Layer dependency reversed

**Default severity:** `warning`

The dependency rule of clean architecture - the layout `aspireforge new` generates - is that
references point inward only:

```
Presentation (Api) -> Infrastructure -> Application -> Domain
```

A reference pointing the other way (`Domain` -> `Infrastructure`, `Application` -> `Api`,
`Infrastructure` -> `Api`, ...) compiles fine, then gradually ties business logic to a database,
framework, or host - exactly what the layering exists to prevent.

In [layered architecture](#architecture-styles) the same rule applies to its own layers:

```
Presentation -> Business -> Data Access -> Entities
```

So `Data Access` -> `Business` and `Entities` -> anything are flagged. Presentation referencing Data
Access directly is allowed in both styles: the host wires data access up for dependency injection.
Using it in code is ARCH001's concern.

ARCH002 walks the project-reference graph starting from the analysed project, so running `doctor`
on the Api checks every layer it pulls in, transitively. Each project's layer comes from the last
segment of its name; see [Architecture styles](#architecture-styles) for the names of each style.
When the architecture isn't recognised, ARCH002 is skipped.

Any other project - `ServiceDefaults`, `Shared`, `Contracts`, an Aspire `AppHost`, and `.Core` - is
unclassified and never reported. `.Core` is deliberately left out: some templates use it for the
domain, others for a shared utilities project. The finding lists each outward reference with the
`.csproj` file and line of its `<ProjectReference>`. Reference cycles and missing project files are
handled without errors.

**Fix:** Move the code the inner layer needs into that layer - or define an interface in the inner
layer (for example `IOrderRepository` in `Application`) and implement it in the outer one
(`Infrastructure`), wiring the two together in the Api's composition root.

## ARCH003 - Inner layer depends on infrastructure framework

**Default severity:** `warning`

[ARCH002](#arch002---layer-dependency-reversed)'s counterpart for packages. A `<PackageReference>`
from `Domain` to EF Core, or from `Application` to Npgsql or ASP.NET Core, is the same layering
violation as a reversed project reference - it ties business logic to a database, web host, or cloud
SDK - but ARCH002 only sees project references.

ARCH003 walks the same project-reference graph from the analysed project, with the same layer naming
as ARCH002, and checks each `Domain` and `Application` project. In layered architecture, `Entities`
is checked like `Domain` and `Business` like `Application`, and the advice names Data Access instead
of Infrastructure. When the architecture isn't recognised, ARCH003 is skipped.

| Dependency | Domain | Application |
| --- | --- | --- |
| EF Core itself - `Microsoft.EntityFrameworkCore`, `.Relational`, `.Abstractions` | flagged | allowed |
| EF Core providers and tooling - `.SqlServer`, `.Sqlite`, `.Cosmos`, `.InMemory`, `.Design`, `.Tools`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Pomelo.*` | flagged | flagged |
| Database drivers - `Npgsql`, `Microsoft.Data.SqlClient`, `MySqlConnector`, `Oracle.*`, `MongoDB.*`, `Dapper`, `StackExchange.Redis` | flagged | flagged |
| ASP.NET Core - `Microsoft.AspNetCore.*` packages, or `<FrameworkReference Include="Microsoft.AspNetCore.App" />` | flagged | flagged |
| HTTP clients - `Microsoft.Extensions.Http`, `Refit`, `RestSharp` | flagged | flagged |
| Messaging and cloud SDKs - `RabbitMQ.*`, `MassTransit`, `Confluent.Kafka`, `Azure.Messaging.*`, `Azure.Storage.*`, `Azure.Data.*`, `AWSSDK.*`, `Google.Cloud.*` | flagged | flagged |

Plain EF Core is allowed in `Application` because many templates deliberately expose `DbSet<T>` from
an Application interface (`IApplicationDbContext`); the *provider* - which decides the database - is
still flagged there. `Microsoft.Extensions.*` abstractions (logging, options, DI, caching) are fine in
both layers, and build-only packages (`PrivateAssets="all"` - analyzers, source generators) are
skipped. Projects outside these two layers (Infrastructure, Api, `ServiceDefaults`, `.Core`, ...) are
never checked. The finding lists each reference with its `.csproj` file and line.

**Fix:** Move the code that needs the package into `Infrastructure` (or the Api), behind an interface
declared in `Application` - for example an `IEmailSender` in Application, implemented with the cloud
SDK in Infrastructure.
