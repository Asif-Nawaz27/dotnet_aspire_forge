using AspireForge.Analyzers.Rules.Architecture;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Architecture;

public class ARCH003Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
    private readonly ARCH003InnerLayerFrameworkDependency _rule = new();

    public ARCH003Tests() => Directory.CreateDirectory(_root);

    // This repository's App: Domain has no packages, Application only Microsoft.Extensions abstractions,
    // Infrastructure and Api take the frameworks.
    [Fact]
    public async Task DoesNotFire_ForACleanArchitectureSolution()
    {
        Project("App.Domain");
        Project("App.Application", ["App.Domain"], """
            <PackageReference Include="Microsoft.Extensions.Caching.Hybrid" Version="10.10.0" />
            <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.12" />
            <PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="10.0.12" />
            """);
        Project("App.Infrastructure", ["App.Application"], """
            <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
            <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.12" />
            """);
        var api = Project("App.Api", ["App.Application", "App.Infrastructure"], """
            <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
            """);

        var issue = await Evaluate(api);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.EntityFrameworkCore.Abstractions")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("Dapper")]
    [InlineData("MongoDB.Driver")]
    [InlineData("StackExchange.Redis")]
    [InlineData("Microsoft.AspNetCore.Http.Abstractions")]
    [InlineData("Microsoft.Extensions.Http")]
    [InlineData("Azure.Messaging.ServiceBus")]
    [InlineData("AWSSDK.S3")]
    public async Task Fires_ForInfrastructurePackagesInDomain_WithFileAndLine(string package)
    {
        var domain = Project("App.Domain", packages: $"""<PackageReference Include="{package}" Version="1.0.0" />""");
        var api = Project("App.Api", ["App.Domain"]);

        var issue = await Evaluate(api);

        Assert.NotNull(issue);
        Assert.Equal("ARCH003", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Equal(domain, issue.FilePath);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains($"App.Domain (Domain) references {package} at App.Domain.csproj:3", issue.Description);
    }

    [Fact]
    public async Task Fires_ForTheAspNetCoreFrameworkReference()
    {
        Project("App.Domain", packages: """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        var api = Project("App.Api", ["App.Domain"]);

        var issue = await Evaluate(api);

        Assert.Contains("references Microsoft.AspNetCore.App (framework)", issue!.Description);
    }

    // Many templates expose DbSet<T> from an Application interface, so plain EF Core is allowed there.
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.EntityFrameworkCore.Relational")]
    public async Task AllowsPlainEfCore_InApplication(string package)
    {
        Project("App.Application", packages: $"""<PackageReference Include="{package}" Version="10.0.12" />""");
        var api = Project("App.Api", ["App.Application"]);

        var issue = await Evaluate(api);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Microsoft.EntityFrameworkCore.Design")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Microsoft.AspNetCore.Mvc.Core")]
    [InlineData("Refit")]
    public async Task Fires_ForProvidersAndWebPackages_InApplication(string package)
    {
        Project("App.Application", packages: $"""<PackageReference Include="{package}" Version="1.0.0" />""");
        var api = Project("App.Api", ["App.Application"]);

        var issue = await Evaluate(api);

        Assert.Contains($"App.Application (Application) references {package}", issue!.Description);
    }

    [Theory]
    [InlineData("""<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" PrivateAssets="all" />""")]
    [InlineData("""<PackageReference Include="Some.Analyzers" Version="1.0.0"><PrivateAssets>all</PrivateAssets></PackageReference>""")]
    [InlineData("""<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.12" />""")]
    [InlineData("""<FrameworkReference Include="Microsoft.WindowsDesktop.App" />""")]
    public async Task DoesNotFire_ForBuildOnlyOrAbstractionPackages(string reference)
    {
        Project("App.Domain", packages: reference);
        var api = Project("App.Api", ["App.Domain"]);

        var issue = await Evaluate(api);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("App.Infrastructure")]
    [InlineData("App.Api")]
    [InlineData("App.ServiceDefaults")]
    [InlineData("App.Core")]
    public async Task IgnoresOuterAndUnclassifiedProjects(string name)
    {
        var project = Project(name, packages: """<PackageReference Include="Npgsql" Version="10.0.0" />""");

        var issue = await Evaluate(project);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ChecksTheWholeGraph_FromTheApi()
    {
        Project("App.Domain", packages: """<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.12" />""");
        Project("App.Application", ["App.Domain"], """<PackageReference Include="StackExchange.Redis" Version="2.8.0" />""");
        Project("App.Infrastructure", ["App.Application"]);
        var api = Project("App.Api", ["App.Infrastructure"]);

        var issue = await Evaluate(api);

        Assert.Contains("App.Domain (Domain) references Microsoft.EntityFrameworkCore", issue!.Description);
        Assert.Contains("App.Application (Application) references StackExchange.Redis", issue.Description);
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
    private string Project(string name, string[]? references = null, string packages = "")
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
