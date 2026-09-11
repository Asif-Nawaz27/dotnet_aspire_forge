using AspireForge.Core.Features;

namespace AspireForge.Generators.Project;

public sealed class GitIgnoreFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "git",
        Name = "Git ignore",
        Description = "Adds a .gitignore tuned for a multi-project .NET solution.",
    };

    public async Task<IReadOnlyList<string>> InstallAsync(
        FeatureContext context, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(context.RootPath, ".gitignore");
        await File.WriteAllTextAsync(path, GitIgnoreTemplate.Render(), cancellationToken);

        return ["Added .gitignore"];
    }
}
