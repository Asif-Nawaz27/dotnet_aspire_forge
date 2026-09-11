using AspireForge.Core.Features;
using AspireForge.Generators.Caching;
using AspireForge.Generators.Database;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Docker;

public sealed class DockerFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "docker",
        Name = "Docker",
        Description = "Adds a Dockerfile, .dockerignore, and a docker-compose.yml wired to whatever "
            + "databases/caches are already configured.",
    };

    public async Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var apiProjectName = $"{context.ProjectName}.Api";
        var appsettingsPath = Path.Combine(CleanArchitectureLayout.ApiProjectPath(context), "appsettings.json");

        await File.WriteAllTextAsync(
            Path.Combine(context.RootPath, "Dockerfile"),
            DockerAssets.Dockerfile(apiProjectName),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(context.RootPath, ".dockerignore"),
            DockerAssets.DockerIgnore(),
            cancellationToken);

        var includePostgres = AppSettingsEditor.HasConnectionString(appsettingsPath, DatabaseProviders.Postgres.ConnectionStringName);
        var includeRedis = AppSettingsEditor.HasConnectionString(appsettingsPath, RedisFeatureInstaller.ConnectionStringName);

        await File.WriteAllTextAsync(
            Path.Combine(context.RootPath, "docker-compose.yml"),
            DockerAssets.DockerCompose(context.ProjectName, includePostgres, includeRedis),
            cancellationToken);

        return ["Added Dockerfile", "Added .dockerignore", "Added docker-compose.yml"];
    }
}
