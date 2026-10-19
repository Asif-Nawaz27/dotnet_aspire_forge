using AspireForge.Analyzers.Rules.Architecture;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Architecture;

public class ARCH002Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
    private readonly ARCH002LayerDependencyReversed _rule = new();

    // The layout `aspireforge new` generates - and this repository's App.
    [Fact]
    public async Task DoesNotFire_ForACleanArchitectureGraph()
    {
        Project("App.Domain");
        Project("App.Application", "App.Domain");
        Project("App.Infrastructure", "App.Domain", "App.Application");
        Project("App.ServiceDefaults");
        var api = Project("App.Api", "App.Application", "App.Infrastructure", "App.ServiceDefaults");

        var issue = await Evaluate(api);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("App.Domain", "App.Infrastructure", "App.Domain (Domain) references App.Infrastructure (Infrastructure)")]
    [InlineData("App.Domain", "App.Application", "App.Domain (Domain) references App.Application (Application)")]
    [InlineData("App.Application", "App.Infrastructure", "App.Application (Application) references App.Infrastructure (Infrastructure)")]
    [InlineData("App.Application", "App.Api", "App.Application (Application) references App.Api (Presentation)")]
    [InlineData("App.Infrastructure", "App.Api", "App.Infrastructure (Infrastructure) references App.Api (Presentation)")]
    public async Task Fires_ForAReferencePointingOutward_AnywhereInTheGraph(string from, string to, string expected)
    {
        Project("App.Domain", from == "App.Domain" ? [to] : []);
        Project("App.Application", from == "App.Application" ? ["App.Domain", to] : ["App.Domain"]);
        Project("App.Infrastructure", from == "App.Infrastructure" ? ["App.Application", to] : ["App.Application"]);
        var api = Project("App.Api", "App.Application", "App.Infrastructure");

        var issue = await Evaluate(api);

        Assert.NotNull(issue);
        Assert.Equal("ARCH002", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Contains(expected, issue.Description);
        Assert.EndsWith($"{from}.csproj", issue.FilePath);
    }

    [Fact]
    public async Task ReportsTheLineOfTheOffendingReference()
    {
        Project("App.Infrastructure");
        var domain = Project("App.Domain", "App.Infrastructure");

        var issue = await Evaluate(domain);

        // Line 1 <Project>, 2 <ItemGroup>, 3 the reference.
        Assert.Equal(3, issue!.LineNumber);
        Assert.Contains("App.Domain.csproj:3", issue.Description);
    }

    [Theory]
    [InlineData("Orders.Persistence", "Infrastructure")]
    [InlineData("Orders.WebApi", "Presentation")]
    [InlineData("Orders.Web", "Presentation")]
    public async Task RecognisesAlternativeLayerNames(string target, string layer)
    {
        Project(target);
        var application = Project("Orders.Application", target);

        var issue = await Evaluate(application);

        Assert.Contains($"{target} ({layer})", issue!.Description);
    }

    [Theory]
    [InlineData("App.Domain", "App.Core")]
    [InlineData("App.Domain", "App.Shared")]
    [InlineData("App.Application", "App.Contracts")]
    [InlineData("App.Infrastructure", "App.ServiceDefaults")]
    [InlineData("App.AppHost", "App.Api")]
    public async Task IgnoresUnclassifiedProjects(string from, string to)
    {
        Project(to);
        var start = Project(from, to);

        var issue = await Evaluate(start);

        Assert.Null(issue);
    }

    [Fact]
    public async Task HandlesCyclesAndMissingProjects()
    {
        Project("App.Application", "App.Infrastructure", "../Missing/App.Missing.csproj");
        var infrastructure = Project("App.Infrastructure", "App.Application");

        var issue = await Evaluate(infrastructure);

        var violation = Assert.Single(issue!.Description.Split(';'));
        Assert.Contains("App.Application (Application) references App.Infrastructure (Infrastructure)", violation);
    }

    [Fact]
    public async Task ReadsLegacyProjectsUsingTheMsBuildNamespace()
    {
        Project("App.Infrastructure");
        var domain = Path.Combine(_root, "App.Domain", "App.Domain.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(domain)!);
        File.WriteAllText(domain, """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup>
                <ProjectReference Include="..\App.Infrastructure\App.Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """);

        var issue = await Evaluate(domain);

        Assert.NotNull(issue);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    // Writes <_root>/<name>/<name>.csproj referencing each sibling by relative path (or a raw path).
    private string Project(string name, params string[] references)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"{name}.csproj");

        var items = references.Select(reference => reference.EndsWith(".csproj", StringComparison.Ordinal)
            ? $"""    <ProjectReference Include="{reference}" />"""
            : $"""    <ProjectReference Include="..\{reference}\{reference}.csproj" />""");

        File.WriteAllText(file, "<Project>\n  <ItemGroup>\n" + string.Join("\n", items) + "\n  </ItemGroup>\n</Project>\n");
        return file;
    }

    private Task<AnalysisIssue?> Evaluate(string projectFile) =>
        _rule.EvaluateAsync(new ProjectContext
        {
            RootDirectory = Path.GetDirectoryName(projectFile)!,
            ProjectFile = projectFile,
            ProjectName = Path.GetFileNameWithoutExtension(projectFile),
            SourceFiles = [],
            ProjectReferences = [],
            PackageReferences = [],
        });
}
