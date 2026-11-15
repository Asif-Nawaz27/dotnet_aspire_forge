using System.Text.Json;
using AspireForge.Core.Features;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Database;

public sealed class EfCoreDatabaseFeatureInstaller(DatabaseProvider provider) : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = provider.Id,
        Name = provider.DisplayName,
        Description =
            $"Adds {provider.EfCorePackageName}, an EF Core DbContext, connection configuration, and migrations support.",
    };

    public async Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var steps = new List<string>();

        var infrastructureCsproj = CleanArchitectureLayout.InfrastructureCsproj(context);
        var apiCsproj = CleanArchitectureLayout.ApiCsproj(context);
        var infrastructureProjectPath = CleanArchitectureLayout.InfrastructureProjectPath(context);
        var apiProjectPath = CleanArchitectureLayout.ApiProjectPath(context);

        FrameworkPackages.Add(infrastructureCsproj, provider.EfCorePackageName, context.RootPath);
        steps.Add($"Added {provider.DisplayName} provider");

        FrameworkPackages.Add(infrastructureCsproj, "Microsoft.EntityFrameworkCore.Design", context.RootPath);
        steps.Add("Added EF Core configuration");

        // The provider package and EFCore.Design can independently resolve to different
        // Microsoft.EntityFrameworkCore patch versions, which breaks the build once Api references
        // Infrastructure. Pin both projects to the same resolved version so the whole solution agrees
        // on one EF Core.
        PinConsistentEfCoreVersion(context, infrastructureCsproj, apiCsproj);

        await WriteDbContext(context, infrastructureProjectPath, cancellationToken);
        steps.Add("Added DbContext");

        WireConnectionConfiguration(context, apiProjectPath);
        steps.Add("Added connection configuration");

        InstallMigrationsTooling(context.RootPath);
        steps.Add("Added migrations support");

        return steps;
    }

    private async Task WriteDbContext(
        FeatureContext context, string infrastructureProjectPath, CancellationToken cancellationToken)
    {
        var persistenceDirectory = Path.Combine(infrastructureProjectPath, "Persistence");
        Directory.CreateDirectory(persistenceDirectory);

        var content = $$"""
            using Microsoft.EntityFrameworkCore;

            namespace {{context.ProjectName}}.Infrastructure.Persistence;

            public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
            {
            }
            """;

        await File.WriteAllTextAsync(Path.Combine(persistenceDirectory, "AppDbContext.cs"), content, cancellationToken);

        // The registration lives in Infrastructure, next to the DbContext, so the Api never touches EF Core
        // itself (ARCH001): Program.cs only passes the connection string in. EnableRetryOnFailure retries
        // transient errors - a failover, a dropped connection - instead of failing the request (REL006).
        var registration = $$"""
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{context.ProjectName}}.Infrastructure.Persistence;

            public static class PersistenceServiceCollectionExtensions
            {
                public static IServiceCollection AddPersistence(this IServiceCollection services, string? connectionString) =>
                    services.AddDbContext<AppDbContext>(options =>
                        // Retries transient failures, such as a failover or a dropped connection.
                        options.{{provider.UseMethodName}}(connectionString, database => database.EnableRetryOnFailure()));
            }
            """;

        await File.WriteAllTextAsync(
            Path.Combine(persistenceDirectory, "PersistenceServiceCollectionExtensions.cs"), registration, cancellationToken);
    }

    // appsettings.json ships in every publish output and container image, so it only gets an empty entry
    // (SEC004): every deployed environment supplies ConnectionStrings__{Name}. The local container's
    // connection string goes into appsettings.Development.json, so `dotnet run` works out of the box.
    // The empty entry also tells `add docker` which databases the project uses.
    private void WireConnectionConfiguration(FeatureContext context, string apiProjectPath)
    {
        AppSettingsEditor.AddConnectionString(
            Path.Combine(apiProjectPath, "appsettings.json"), provider.ConnectionStringName, string.Empty);

        AppSettingsEditor.AddConnectionString(
            Path.Combine(apiProjectPath, "appsettings.Development.json"),
            provider.ConnectionStringName,
            provider.LocalConnectionString(context.ProjectName, "localhost"));

        ProgramFileEditor.AddUsingsAndServiceRegistration(
            Path.Combine(apiProjectPath, "Program.cs"),
            usings: [$"{context.ProjectName}.Infrastructure.Persistence"],
            serviceRegistrationLines:
            [
                $"builder.Services.AddPersistence(builder.Configuration.GetConnectionString(\"{provider.ConnectionStringName}\"));",
            ]);
    }

    private static void PinConsistentEfCoreVersion(FeatureContext context, string infrastructureCsproj, string apiCsproj)
    {
        var version = ReadResolvedPackageVersion(infrastructureCsproj, "Microsoft.EntityFrameworkCore.Design");

        DotnetCli.Run(["add", infrastructureCsproj, "package", "Microsoft.EntityFrameworkCore", "--version", version], context.RootPath);
        DotnetCli.Run(["add", apiCsproj, "package", "Microsoft.EntityFrameworkCore", "--version", version], context.RootPath);
    }

    // Reads the version NuGet actually resolved, from project.assets.json rather than the raw
    // PackageReference XML. The XML's Version attribute only exists when the project sets its own
    // version - under central package management (a Directory.Packages.props reachable from this
    // project, common in monorepos) `dotnet add package` omits it entirely, since the version lives
    // in Directory.Packages.props instead. project.assets.json's "libraries" section, by contrast,
    // always lists the fully-resolved version post-restore ("PackageId/Version" keys) regardless of
    // where that version was declared, so this works the same way with or without CPM.
    private static string ReadResolvedPackageVersion(string csprojPath, string packageName)
    {
        var assetsPath = Path.Combine(Path.GetDirectoryName(csprojPath)!, "obj", "project.assets.json");

        if (!File.Exists(assetsPath))
        {
            throw new InvalidOperationException(
                $"Could not determine the resolved EF Core version: '{assetsPath}' does not exist. Expected the project to have been restored by the preceding `dotnet add package`.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));

        if (document.RootElement.TryGetProperty("libraries", out var libraries))
        {
            foreach (var library in libraries.EnumerateObject())
            {
                var separator = library.Name.LastIndexOf('/');

                if (separator > 0
                    && string.Equals(library.Name[..separator], packageName, StringComparison.OrdinalIgnoreCase))
                {
                    return library.Name[(separator + 1)..];
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not determine the resolved version of '{packageName}' from '{assetsPath}'.");
    }

    private static void InstallMigrationsTooling(string root)
    {
        var manifestPath = Path.Combine(root, "dotnet-tools.json");

        if (!File.Exists(manifestPath))
        {
            DotnetCli.Run(["new", "tool-manifest"], root);
        }

        DotnetCli.Run(["tool", "install", "dotnet-ef"], root);
    }
}
