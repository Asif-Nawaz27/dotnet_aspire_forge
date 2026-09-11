namespace AspireForge.Generators.Database;

// Only Postgres is wired up for now. SQL Server and MySQL become additional entries here
// (Microsoft.EntityFrameworkCore.SqlServer/UseSqlServer, Pomelo.EntityFrameworkCore.MySql/UseMySql)
// once they're actually implemented - EfCoreDatabaseFeatureInstaller doesn't need to change.
public static class DatabaseProviders
{
    public static readonly DatabaseProvider Postgres = new(
        Id: "postgres",
        DisplayName: "PostgreSQL",
        ConnectionStringName: "Postgres",
        EfCorePackageName: "Npgsql.EntityFrameworkCore.PostgreSQL",
        UseMethodName: "UseNpgsql",
        ConnectionString: projectName => $"Host=localhost;Database={projectName};Username=postgres;Password=postgres");
}
