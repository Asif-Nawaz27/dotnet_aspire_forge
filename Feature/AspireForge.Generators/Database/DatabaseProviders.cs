namespace AspireForge.Generators.Database;

// MySQL becomes another entry here (Pomelo.EntityFrameworkCore.MySql/UseMySql) once it's actually
// implemented - EfCoreDatabaseFeatureInstaller doesn't need to change.
//
// The credentials match the database containers in docker-compose.yml (DockerAssets).
public static class DatabaseProviders
{
    public static readonly DatabaseProvider Postgres = new(
        Id: "postgres",
        DisplayName: "PostgreSQL",
        ConnectionStringName: "Postgres",
        EfCorePackageName: "Npgsql.EntityFrameworkCore.PostgreSQL",
        UseMethodName: "UseNpgsql",
        LocalConnectionString: (projectName, host) =>
            $"Host={host};Database={projectName};Username=postgres;Password=postgres");

    public static readonly DatabaseProvider SqlServer = new(
        Id: "sqlserver",
        DisplayName: "SQL Server",
        ConnectionStringName: "SqlServer",
        EfCorePackageName: "Microsoft.EntityFrameworkCore.SqlServer",
        UseMethodName: "UseSqlServer",
        // TrustServerCertificate: the local container presents a self-signed certificate (SEC010
        // doesn't scan appsettings.Development.json).
        LocalConnectionString: (projectName, host) =>
            $"Server={host};Database={projectName};User Id=sa;Password=Your_password123;TrustServerCertificate=True;");
}
