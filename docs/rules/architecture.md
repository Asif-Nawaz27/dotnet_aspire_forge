# Architecture rules

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

ARCH002 walks the project-reference graph starting from the analysed project, so running `doctor`
on the Api checks every layer it pulls in, transitively. Each project's layer comes from the last
segment of its name:

| Layer | Project name ends in |
| --- | --- |
| Domain | `.Domain` |
| Application | `.Application` |
| Infrastructure | `.Infrastructure`, `.Persistence` |
| Presentation | `.Api`, `.WebApi`, `.Web` |

Any other project - `ServiceDefaults`, `Shared`, `Contracts`, an Aspire `AppHost`, and `.Core` - is
unclassified and never reported. `.Core` is deliberately left out: some templates use it for the
domain, others for a shared utilities project. The finding lists each outward reference with the
`.csproj` file and line of its `<ProjectReference>`. Reference cycles and missing project files are
handled without errors.

**Fix:** Move the code the inner layer needs into that layer - or define an interface in the inner
layer (for example `IOrderRepository` in `Application`) and implement it in the outer one
(`Infrastructure`), wiring the two together in the Api's composition root.
