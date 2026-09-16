using AspireForge.Core.Features;
using AspireForge.Core.Generation;
using AspireForge.Generators;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Tests;

public class UpdateFeatureSetTests : IDisposable
{
    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Update_RestoresScaffoldingFiles_ThatWereDeleted()
    {
        var context = Generate("SampleApi");

        File.Delete(Path.Combine(context.RootPath, ".gitignore"));
        File.Delete(Path.Combine(context.RootPath, "docker-compose.yml"));
        File.Delete(Path.Combine(context.RootPath, ".github", "workflows", "ci.yml"));

        foreach (var installer in UpdateFeatureSet.CreateDefault())
        {
            await installer.InstallAsync(context);
        }

        Assert.True(File.Exists(Path.Combine(context.RootPath, ".gitignore")));
        Assert.True(File.Exists(Path.Combine(context.RootPath, "docker-compose.yml")));
        Assert.True(File.Exists(Path.Combine(context.RootPath, ".github", "workflows", "ci.yml")));
    }

    [Fact]
    public async Task Update_DoesNotTouchReadme()
    {
        var context = Generate("SampleApi");
        var readmePath = Path.Combine(context.RootPath, "README.md");

        File.AppendAllText(readmePath, "custom project-specific notes");
        var before = File.ReadAllText(readmePath);

        foreach (var installer in UpdateFeatureSet.CreateDefault())
        {
            await installer.InstallAsync(context);
        }

        Assert.Equal(before, File.ReadAllText(readmePath));
    }

    private FeatureContext Generate(string projectName)
    {
        var generator = new CleanArchitectureGenerator();
        var result = generator.Generate(new GenerationOptions
        {
            ProjectName = projectName,
            Architecture = "clean",
            OutputPath = _outputPath,
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

        return new FeatureContext { ProjectName = projectName, RootPath = Path.Combine(_outputPath, projectName) };
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }
    }
}
