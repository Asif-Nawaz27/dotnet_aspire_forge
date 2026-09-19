using AspireForge.Core.Features;

namespace AspireForge.Generators.GitHub;

public sealed class GitHubActionsFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "github-actions",
        Name = "GitHub Actions CI",
        Description = "Adds a workflow that restores, builds, and tests the solution on push and pull request.",
    };

    public async Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(context.RootPath, ".github", "workflows", "ci.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await File.WriteAllTextAsync(path, GitHubWorkflows.Ci(context.ProjectName), cancellationToken);

        return ["Added CI workflow"];
    }
}
