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

**Fix:** Move the persistence package reference to your `Infrastructure` project and access it
through an abstraction (a repository interface, for example) instead of referencing it from `Api`.
