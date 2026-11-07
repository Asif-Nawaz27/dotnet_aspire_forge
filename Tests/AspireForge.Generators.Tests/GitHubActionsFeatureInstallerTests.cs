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
    public async Task InstallAsync_TheWorkflowPinsTheToolToTheVersionThatGeneratedIt()
    {
        var content = await GenerateWorkflow();

        Assert.Contains($"dotnet tool install --global AspireForge --version {ToolVersion.Current}", content);
        Assert.Contains("echo \"$HOME/.dotnet/tools\" >> \"$GITHUB_PATH\"", content);
    }

    // Regression guard in the spirit of the .slnx test above: the path must be a directory `new` creates.
    [Fact]
    public async Task InstallAsync_RunsDoctorAgainstTheApiProjectThatActuallyExists()
    {
        var content = await GenerateWorkflow();
        var api = Path.Combine(_outputPath, "SampleApi", "src", "SampleApi.Api");

        Assert.True(File.Exists(Path.Combine(api, "SampleApi.Api.csproj")));
        Assert.Contains("aspireforge doctor src/SampleApi.Api --format markdown >> \"$GITHUB_STEP_SUMMARY\" || true", content);
        Assert.Contains("aspireforge doctor src/SampleApi.Api --format sarif > doctor.sarif || true", content);
    }

    // Only the last step may fail the build, and it has to run last: a failure there would otherwise
    // skip the summary and upload, which are most useful exactly when doctor finds something.
    [Fact]
    public async Task InstallAsync_TheDoctorCheckIsTheLastStep_AndTheOnlyOneThatCanFail()
    {
        var content = await GenerateWorkflow();
        var lines = content.ReplaceLineEndings("\n").TrimEnd().Split('\n');

        Assert.Equal("run: aspireforge doctor src/SampleApi.Api", lines[^1].Trim());
        Assert.Equal(
            2,
            lines.Count(line => line.Contains("aspireforge doctor", StringComparison.Ordinal) && line.EndsWith("|| true", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task InstallAsync_AFailedSarifUpload_DoesNotFailTheBuild()
    {
        var content = await GenerateWorkflow();
        var lines = content.ReplaceLineEndings("\n").Split('\n').Select(line => line.Trim()).ToList();
        var upload = lines.FindIndex(line => line.StartsWith("uses: github/codeql-action/upload-sarif@", StringComparison.Ordinal));

        Assert.True(upload >= 0);
        Assert.Equal("continue-on-error: true", lines[upload + 1]);
        Assert.Contains("security-events: write", content);
        Assert.Contains("contents: read", content);
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

    private async Task<string> GenerateWorkflow()
    {
        var context = Generate("SampleApi");

        await new GitHubActionsFeatureInstaller().InstallAsync(context);

        return await File.ReadAllTextAsync(Path.Combine(context.RootPath, ".github", "workflows", "ci.yml"));
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
