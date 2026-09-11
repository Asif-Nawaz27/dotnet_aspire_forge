using AspireForge.Core.Features;

namespace AspireForge.Generators.Project;

public sealed class ReadmeFeatureInstaller : IFeatureInstaller
{
    public FeatureDefinition Feature { get; } = new()
    {
        Id = "readme",
        Name = "README",
        Description = "Adds a README describing the generated layout and how to build/run it.",
    };

    public Task InstallAsync(FeatureContext context, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(context.RootPath, "README.md");
        return File.WriteAllTextAsync(path, ReadmeTemplate.Render(context.ProjectName), cancellationToken);
    }
}
