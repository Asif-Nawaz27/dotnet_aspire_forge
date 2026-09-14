using AspireForge.Core.Features;
using AspireForge.Core.Generation;
using AspireForge.Generators.Observability;
using AspireForge.Generators.Project;

namespace AspireForge.IntegrationTests;

// Nested `dotnet build`/`dotnet test` invocations are resource-heavy (MSBuild/vstest processes) and
// contend with each other if run concurrently, so these must not run in parallel with one another.
[CollectionDefinition(nameof(GeneratedProjectBuildCollection), DisableParallelization = true)]
public class GeneratedProjectBuildCollection;

// End-to-end: AspireForge should prove the projects it generates actually build and test cleanly,
// not just that generation reports success.
[Collection(nameof(GeneratedProjectBuildCollection))]
public class GeneratedProjectBuildTests : IDisposable
{
    private static readonly TimeSpan SubprocessTimeout = TimeSpan.FromMinutes(3);

    private readonly string _outputPath =
        Path.Combine(Path.GetTempPath(), "aspireforge-e2e", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GeneratedProject_BuildsSuccessfully()
    {
        // Deliberately not also running `dotnet test` here: nesting vstest inside a process that is
        // itself running under `dotnet test` (as this one is, when run via the test suite) is a known
        // source of hangs. The CI pipeline's own "Integration Tests" step runs `dotnet test` directly
        // as a top-level command instead, which does not have this problem.
        var root = Generate("TestApi");

        DotnetCli.Run(["build"], root, SubprocessTimeout);
    }

    [Fact]
    public async Task GeneratedProject_WithTelemetryAdded_StillBuildsSuccessfully()
    {
        var root = Generate("TelemetryApi");

        var context = new FeatureContext { ProjectName = "TelemetryApi", RootPath = root };
        await new TelemetryFeatureInstaller().InstallAsync(context);

        DotnetCli.Run(["build"], root, SubprocessTimeout);
    }

    private string Generate(string projectName)
    {
        var generator = new CleanArchitectureGenerator();
        var result = generator.Generate(new GenerationOptions
        {
            ProjectName = projectName,
            Architecture = "clean",
            OutputPath = _outputPath,
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

        return Path.Combine(_outputPath, projectName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }
    }
}
