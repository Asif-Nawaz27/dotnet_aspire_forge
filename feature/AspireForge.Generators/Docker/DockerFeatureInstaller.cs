using AspireForge.Core.Features;

namespace AspireForge.Generators.Docker;

public sealed class DockerFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "docker",
        Name = "Docker",
        Description = "Adds a multi-stage Dockerfile and .dockerignore targeting the generated Api project.",
    };

    public async Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var apiProjectName = $"{context.ProjectName}.Api";

        await File.WriteAllTextAsync(
            Path.Combine(context.RootPath, "Dockerfile"),
            DockerAssets.Dockerfile(apiProjectName),
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(context.RootPath, ".dockerignore"),
            DockerAssets.DockerIgnore(),
            cancellationToken);

        return ["Added Dockerfile", "Added .dockerignore"];
    }
}
