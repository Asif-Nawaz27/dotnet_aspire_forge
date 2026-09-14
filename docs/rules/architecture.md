# Architecture rules

## ARCH001 - API directly accesses persistence layer

**Default severity:** `warning`

Fires when the project is a web app that directly references a persistence package
(`Microsoft.EntityFrameworkCore*`, `Dapper`, `Npgsql*`, `Microsoft.Data.SqlClient`,
`System.Data.SqlClient`, or `MongoDB.Driver`). In the clean architecture layout `aspireforge new`
generates, persistence access should live behind `Infrastructure`, not be referenced directly from
`Api`.

**Fix:** Move the persistence package reference to your `Infrastructure` project and access it
through an abstraction (a repository interface, for example) instead of referencing it from `Api`.

> **Note:** `aspireforge add postgres` currently adds a direct `Microsoft.EntityFrameworkCore`
> reference to `Api` as well as `Infrastructure`, purely to keep both projects resolving the same EF
> Core version (see [postgres](../features/postgres.md)). This can trip ARCH001 even though `Api`
> isn't actually querying the database directly - a known rough edge, not a sign your project is
> structured wrong.
