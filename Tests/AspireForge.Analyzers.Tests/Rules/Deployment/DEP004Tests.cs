using AspireForge.Analyzers.Rules.Deployment;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Deployment;

public class DEP004Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
    private readonly DEP004PrereleasePackages _rule = new();

    public DEP004Tests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task DoesNotFire_ForStableVersions()
    {
        var api = Project("App.Api", packages: """
            <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
            <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.18.0" />
            """);

        var issue = await Evaluate(api);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("10.0.0-preview.7.25380.108")]
    [InlineData("9.0.0-rc.2")]
    [InlineData("1.0.0-beta.12")]
    [InlineData("2.1.0-alpha")]
    [InlineData("1.2.3-ci.20261031.4")]
    public async Task Fires_AsAWarning_ForPrereleaseVersions_WithFileAndLine(string version)
    {
        var api = Project("App.Api", packages: $"""
            <PackageReference Include="Some.Package" Version="{version}" />
            """);

        var issue = await Evaluate(api);

        Assert.NotNull(issue);
        Assert.Equal("DEP004", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Equal(api, issue.FilePath);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains($"Some.Package {version} (prerelease, App.Api.csproj:3)", issue.Description);
    }

    [Theory]
    [InlineData("10.*")]
    [InlineData("*")]
    [InlineData("10.0.0-*")]
    public async Task Suggests_PinningFloatingVersions(string version)
    {
        var api = Project("App.Api", packages: $"""<PackageReference Include="Some.Package" Version="{version}" />""");

        var issue = await Evaluate(api);

        Assert.Equal(Severity.Suggestion, issue!.Severity);
        Assert.Contains("(floating,", issue.Description);
    }

    [Theory]
    [InlineData("""<PackageReference Include="Some.Package"><Version>1.0.0-beta</Version></PackageReference>""")]
    [InlineData("""<PackageReference Include="Some.Package" Version="[1.0.0-beta, )" />""")]
    [InlineData("""<PackageReference Include="Some.Package" VersionOverride="2.0.0-rc.1" />""")]
    public async Task ReadsVersionElementsRangesAndOverrides(string reference)
    {
        var api = Project("App.Api", packages: reference);

        var issue = await Evaluate(api);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("""<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0-rc.1" PrivateAssets="all" />""")]
    [InlineData("""<PackageReference Include="Some.Analyzers" Version="1.0.0-beta"><PrivateAssets>all</PrivateAssets></PackageReference>""")]
    [InlineData("""<PackageReference Include="Some.Package" Version="$(SomeVersion)" />""")]
    [InlineData("""<PackageReference Include="Some.Package" Version="[1.0.0, 2.0.0)" />""")]
    public async Task DoesNotFire_ForBuildOnlyPackagesPropertiesOrStableRanges(string reference)
    {
        var api = Project("App.Api", packages: reference);

        var issue = await Evaluate(api);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ReadsCentralPackageManagement_AndPointsAtTheProps()
    {
        File.WriteAllText(Path.Combine(_root, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Stable.Package" Version="1.0.0" />
                <PackageVersion Include="Preview.Package" Version="2.0.0-preview.1" />
                <PackageVersion Include="Unused.Package" Version="3.0.0-beta" />
              </ItemGroup>
            </Project>
            """);
        var api = Project("App.Api", packages: """
            <PackageReference Include="Stable.Package" />
            <PackageReference Include="Preview.Package" />
            """);

        var issue = await Evaluate(api);

        Assert.EndsWith("Directory.Packages.props", issue!.FilePath);
        Assert.Equal(4, issue.LineNumber);
        Assert.Contains("Preview.Package 2.0.0-preview.1 (prerelease, Directory.Packages.props:4)", issue.Description);
        Assert.DoesNotContain("Unused.Package", issue.Description);
    }

    [Fact]
    public async Task ChecksReferencedProjects_AndReportsASharedCentralVersionOnce()
    {
        File.WriteAllText(Path.Combine(_root, "Directory.Packages.props"),
            """<Project><ItemGroup><PackageVersion Include="Preview.Package" Version="2.0.0-rc.1" /></ItemGroup></Project>""");
        Project("App.Infrastructure", packages: """<PackageReference Include="Preview.Package" />""");
        var api = Project("App.Api", packages: """<PackageReference Include="Preview.Package" />""", references: ["App.Infrastructure"]);

        var issue = await Evaluate(api);

        Assert.Single(issue!.Description.Split("Preview.Package 2.0.0-rc.1").Skip(1));
    }

    [Fact]
    public async Task FindsPrereleasesInReferencedProjects()
    {
        Project("App.Infrastructure", packages: """<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0-rc.2" />""");
        var api = Project("App.Api", packages: """<PackageReference Include="Stable" Version="1.0.0" />""", references: ["App.Infrastructure"]);

        var issue = await Evaluate(api);

        Assert.EndsWith("App.Infrastructure.csproj", issue!.FilePath);
    }

    [Fact]
    public async Task ReportsWarningsBeforeSuggestions()
    {
        var api = Project("App.Api", packages: """
            <PackageReference Include="A.Floating" Version="1.*" />
            <PackageReference Include="Z.Preview" Version="1.0.0-beta" />
            """);

        var issue = await Evaluate(api);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.StartsWith("Packages that ship with this service aren't pinned to stable releases: Z.Preview", issue.Description);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    // <_root>/<name>/<name>.csproj. Line 1 <Project>, 2 <ItemGroup>, 3 the first package.
    private string Project(string name, string packages, string[]? references = null)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"{name}.csproj");

        var projectReferences = string.Join("\n", (references ?? []).Select(r => $"""    <ProjectReference Include="..\{r}\{r}.csproj" />"""));
        File.WriteAllText(file, $"<Project>\n  <ItemGroup>\n{packages}\n{projectReferences}\n  </ItemGroup>\n</Project>\n");
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
