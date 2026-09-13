using AspireForge.Analyzers.Rules.Testing;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Testing;

public class TEST001Tests
{
    private readonly TEST001NoTestProjectDetected _rule = new();

    [Fact]
    public async Task Fires_WhenNoMatchingTestProjectExistsUnderTests()
    {
        using var solution = SolutionLayout.Create("MyApi", testProjectNames: []);

        var issue = await _rule.EvaluateAsync(solution.ApiContext);

        Assert.NotNull(issue);
        Assert.Equal("TEST001", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenAMatchingTestProjectExists()
    {
        using var solution = SolutionLayout.Create("MyApi", testProjectNames: ["MyApi.Api.Tests"]);

        var issue = await _rule.EvaluateAsync(solution.ApiContext);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_ForATestProjectItself()
    {
        using var solution = SolutionLayout.Create("MyApi", testProjectNames: []);

        var testContext = new ProjectContext
        {
            RootDirectory = solution.ApiContext.RootDirectory,
            ProjectFile = solution.ApiContext.ProjectFile,
            ProjectName = "MyApi.Tests",
            SourceFiles = solution.ApiContext.SourceFiles,
            ProjectReferences = solution.ApiContext.ProjectReferences,
            PackageReferences = solution.ApiContext.PackageReferences,
        };

        var issue = await _rule.EvaluateAsync(testContext);

        Assert.Null(issue);
    }
}

internal sealed class SolutionLayout : IDisposable
{
    private readonly string _root;

    private SolutionLayout(string root, ProjectContext apiContext)
    {
        _root = root;
        ApiContext = apiContext;
    }

    public ProjectContext ApiContext { get; }

    public static SolutionLayout Create(string projectName, IReadOnlyList<string> testProjectNames)
    {
        var root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
        var apiDirectory = Path.Combine(root, "src", $"{projectName}.Api");
        Directory.CreateDirectory(apiDirectory);

        var testsDirectory = Path.Combine(root, "tests");
        Directory.CreateDirectory(testsDirectory);

        foreach (var testProjectName in testProjectNames)
        {
            Directory.CreateDirectory(Path.Combine(testsDirectory, testProjectName));
        }

        var apiContext = new ProjectContext
        {
            RootDirectory = apiDirectory,
            ProjectFile = Path.Combine(apiDirectory, $"{projectName}.Api.csproj"),
            ProjectName = $"{projectName}.Api",
            SourceFiles = [],
            ProjectReferences = [],
            PackageReferences = [],
        };

        return new SolutionLayout(root, apiContext);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
