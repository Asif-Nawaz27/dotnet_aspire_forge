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

    public Task InstallAsync(FeatureContext context, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(context.RootPath, ".github", "workflows", "ci.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        return File.WriteAllTextAsync(path, GitHubWorkflows.Ci(context.ProjectName), cancellationToken);
    }
}
