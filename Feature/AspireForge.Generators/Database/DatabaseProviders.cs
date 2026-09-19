namespace AspireForge.Generators.Database;

// MySQL becomes another entry here (Pomelo.EntityFrameworkCore.MySql/UseMySql) once it's actually
// implemented - EfCoreDatabaseFeatureInstaller doesn't need to change.
public static class DatabaseProviders
{
    public static readonly DatabaseProvider Postgres = new(
        Id: "postgres",
        DisplayName: "PostgreSQL",
        ConnectionStringName: "Postgres",
        EfCorePackageName: "Npgsql.EntityFrameworkCore.PostgreSQL",
        UseMethodName: "UseNpgsql",
        ConnectionString: projectName => $"Host=localhost;Database={projectName};Username=postgres;Password=postgres");

    public static readonly DatabaseProvider SqlServer = new(
        Id: "sqlserver",
        DisplayName: "SQL Server",
        ConnectionStringName: "SqlServer",
        EfCorePackageName: "Microsoft.EntityFrameworkCore.SqlServer",
        UseMethodName: "UseSqlServer",
        ConnectionString: projectName =>
            $"Server=localhost;Database={projectName};User Id=sa;Password=Your_password123;TrustServerCertificate=True;");
}
