using AspireForge.Core.Features;
using AspireForge.Core.Generation;
using AspireForge.Generators.GitHub;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Tests;

public class GitHubActionsFeatureInstallerTests : IDisposable
{
    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    // Regression test: the generated workflow previously hardcoded "{projectName}.sln", but `new`
    // generates a "{projectName}.slnx" solution - every generated project's CI failed on the very
    // first "dotnet restore" as a result. Assert against the solution file that's actually on disk,
    // not just the string ".slnx", so a future mismatch in either direction fails this test.
    [Fact]
    public async Task InstallAsync_ReferencesTheSolutionFileThatActuallyExists()
    {
        var context = Generate("SampleApi");

        await new GitHubActionsFeatureInstaller().InstallAsync(context);

        var solutionFile = Directory.EnumerateFiles(context.RootPath, "*.sln*", SearchOption.TopDirectoryOnly).Single();
        var workflowContent = await File.ReadAllTextAsync(Path.Combine(context.RootPath, ".github", "workflows", "ci.yml"));

        Assert.Contains(Path.GetFileName(solutionFile), workflowContent);
    }

    [Fact]
    public async Task InstallAsync_WritesTheWorkflowFileToTheConventionalGitHubActionsPath()
    {
        var context = Generate("SampleApi");

        await new GitHubActionsFeatureInstaller().InstallAsync(context);

        Assert.True(File.Exists(Path.Combine(context.RootPath, ".github", "workflows", "ci.yml")));
    }

    [Fact]
    public async Task InstallAsync_TheWorkflowRestoresBuildsAndTestsTheSolution()
    {
        var context = Generate("SampleApi");

        await new GitHubActionsFeatureInstaller().InstallAsync(context);

        var solutionFileName = Path.GetFileName(
            Directory.EnumerateFiles(context.RootPath, "*.sln*", SearchOption.TopDirectoryOnly).Single());
        var content = await File.ReadAllTextAsync(Path.Combine(context.RootPath, ".github", "workflows", "ci.yml"));

        Assert.Contains($"dotnet restore {solutionFileName}", content);
        Assert.Contains($"dotnet build {solutionFileName}", content);
        Assert.Contains($"dotnet test {solutionFileName}", content);
    }

    [Fact]
    public async Task InstallAsync_TheWorkflowTriggersOnPushAndPullRequestToMain()
    {
        var context = Generate("SampleApi");

        await new GitHubActionsFeatureInstaller().InstallAsync(context);

        var content = await File.ReadAllTextAsync(Path.Combine(context.RootPath, ".github", "workflows", "ci.yml"));

        Assert.Contains("push:", content);
        Assert.Contains("pull_request:", content);
        Assert.Contains("branches: [ main ]", content);
    }

    [Fact]
    public async Task InstallAsync_IsIdempotent_SoUpdateCanSafelyReRunIt()
    {
        var context = Generate("SampleApi");
        var installer = new GitHubActionsFeatureInstaller();

        await installer.InstallAsync(context);
        var steps = await installer.InstallAsync(context);

        Assert.Single(steps);
        Assert.True(File.Exists(Path.Combine(context.RootPath, ".github", "workflows", "ci.yml")));
    }

    [Fact]
    public void Feature_IsRegisteredUnderTheExpectedId()
    {
        var installer = new GitHubActionsFeatureInstaller();

        Assert.Equal("github-actions", installer.Feature.Id);
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
