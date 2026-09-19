namespace AspireForge.Generators.Database;

// The provider-specific slice of the ASP.NET Core -> Application -> Infrastructure -> EF Core ->
// [provider] -> [database] pipeline. EfCoreDatabaseFeatureInstaller implements everything above
// EF Core once; a new database only needs one of these.
public sealed record DatabaseProvider(
    string Id,
    string DisplayName,
    string ConnectionStringName,
    string EfCorePackageName,
    string UseMethodName,
    Func<string, string> ConnectionString);
