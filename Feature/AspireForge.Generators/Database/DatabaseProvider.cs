namespace AspireForge.Generators.Database;

// The provider-specific slice of the ASP.NET Core -> Application -> Infrastructure -> EF Core ->
// [provider] -> [database] pipeline. EfCoreDatabaseFeatureInstaller implements everything above
// EF Core once; a new database only needs one of these.
//
// LocalConnectionString is the connection string for the local database container that docker-compose
// starts (credentials included), given the project name and the host: "localhost" from the developer's
// machine, the compose service name (Id) from the api container. It only ever goes into
// appsettings.Development.json and docker-compose.yml, never into appsettings.json (SEC004).
public sealed record DatabaseProvider(
    string Id,
    string DisplayName,
    string ConnectionStringName,
    string EfCorePackageName,
    string UseMethodName,
    Func<string, string, string> LocalConnectionString);
