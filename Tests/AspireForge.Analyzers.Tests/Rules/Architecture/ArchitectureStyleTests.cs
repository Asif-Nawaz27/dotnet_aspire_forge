using AspireForge.Analyzers.Rules.Architecture;
using AspireForge.Core.Analysis;
using AspireForge.Core.Configuration;

namespace AspireForge.Analyzers.Tests.Rules.Architecture;

// Detection, and the architecture rules under the layered style. Clean architecture is covered by each
// rule's own tests, which run with the architecture detected - the same as before styles existed.
public class ArchitectureStyleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    public ArchitectureStyleTests() => Directory.CreateDirectory(_root);

    // ---- Detection

    [Fact]
    public void Detects_Clean_ForTheLayoutNewGenerates()
    {
        Project("Shop.Domain");
        Project("Shop.Application", ["Shop.Domain"]);
        Project("Shop.Infrastructure", ["Shop.Application"]);
        Project("Shop.ServiceDefaults");
        var api = Project("Shop.Api", ["Shop.Application", "Shop.Infrastructure", "Shop.ServiceDefaults"]);

        var resolution = ArchitectureResolver.Resolve(api, configured: null);

        Assert.Equal(ArchitectureModel.Clean, resolution.Model);
        Assert.Equal("clean (detected)", resolution.Describe());
    }

    [Theory]
    [InlineData("Shop.Web", "Shop.BLL", "Shop.DAL", "Shop.Entities")]
    [InlineData("Shop.Api", "Shop.Business", "Shop.DataAccess", "Shop.Models")]
    [InlineData("Shop.Mvc", "Shop.BusinessLogic", "Shop.Data", "Shop.Domain")]
    [InlineData("Shop.UI", "Shop.Logic", "Shop.Repositories", "Shop.Entities")]
    public void Detects_Layered_ForCommonNTierNames(string presentation, string business, string data, string entities)
    {
        Project(entities);
        Project(data, [entities]);
        Project(business, [data, entities]);
        var top = Project(presentation, [business, entities]);

        Assert.Equal(ArchitectureModel.Layered, ArchitectureResolver.Resolve(top, configured: null).Model);
    }

    [Fact]
    public void Detects_IgnoringCase()
    {
        Project("shop.dal");
        var top = Project("shop.bll", ["shop.dal"]);

        Assert.Equal(ArchitectureModel.Layered, ArchitectureResolver.Resolve(top, configured: null).Model);
    }

    // Running doctor on a Domain project alone was checked as clean before styles existed.
    [Fact]
    public void Detects_Clean_ForADomainProjectOnItsOwn() =>
        Assert.Equal(ArchitectureModel.Clean, ArchitectureResolver.Resolve(Project("Shop.Domain"), configured: null).Model);

    // A clean solution with one project called Data is still clean.
    [Fact]
    public void Detects_TheStyleWithMoreSignatureNames()
    {
        Project("Shop.Domain");
        Project("Shop.Data");
        Project("Shop.Application", ["Shop.Domain"]);
        Project("Shop.Infrastructure", ["Shop.Application", "Shop.Data"]);
        var api = Project("Shop.Api", ["Shop.Infrastructure"]);

        Assert.Equal(ArchitectureModel.Clean, ArchitectureResolver.Resolve(api, configured: null).Model);
    }

    [Theory]
    [InlineData("Shop")]
    [InlineData("Shop.Api")]
    [InlineData("Shop.Api", "Shop.Worker", "Shop.Shared")]
    [InlineData("Shop.Api", "Shop.Services", "Shop.Models")]
    public void DoesNotGuess_WhenNoNameSaysWhichStyle(string start, params string[] references)
    {
        foreach (var reference in references)
        {
            Project(reference);
        }

        var resolution = ArchitectureResolver.Resolve(Project(start, references), configured: null);

        Assert.Null(resolution.Model);
        Assert.Equal("not recognised", resolution.Describe());
    }

    [Fact]
    public void DoesNotGuess_WhenTheStylesTie()
    {
        Project("Shop.DAL");
        var top = Project("Shop.Application", ["Shop.DAL"]);

        Assert.Null(ArchitectureResolver.Resolve(top, configured: null).Model);
    }

    [Fact]
    public void Config_WinsOverDetection()
    {
        Project("Shop.Application");
        var top = Project("Shop.Api", ["Shop.Application"]);

        var resolution = ArchitectureResolver.Resolve(top, ArchitectureStyle.Layered);

        Assert.Equal(ArchitectureModel.Layered, resolution.Model);
        Assert.Equal("layered (from config)", resolution.Describe());
    }

    [Fact]
    public void Config_AppliesWhereNothingWouldBeDetected()
    {
        Project("Shop.Services");
        var top = Project("Shop.Api", ["Shop.Services"]);

        Assert.Equal(ArchitectureModel.Layered, ArchitectureResolver.Resolve(top, ArchitectureStyle.Layered).Model);
    }

    // ---- ARCH002 under layered

    [Fact]
    public async Task ARCH002_AcceptsAWellFormedLayeredSolution()
    {
        var web = LayeredSolution();

        Assert.Null(await new ARCH002LayerDependencyReversed().EvaluateAsync(Context(web)));
    }

    [Theory]
    [InlineData("Shop.DAL", "Shop.BLL", "Shop.DAL (Data Access) references Shop.BLL (Business)")]
    [InlineData("Shop.Entities", "Shop.DAL", "Shop.Entities (Entities) references Shop.DAL (Data Access)")]
    [InlineData("Shop.BLL", "Shop.Web", "Shop.BLL (Business) references Shop.Web (Presentation)")]
    public async Task ARCH002_FlagsAReversedLayeredReference(string from, string to, string expected)
    {
        var web = LayeredSolution(extra: (from, to));

        var issue = await new ARCH002LayerDependencyReversed().EvaluateAsync(Context(web));

        Assert.NotNull(issue);
        Assert.Contains(expected, issue!.Description);
        Assert.Contains("In layered architecture, dependencies must point inward (Presentation -> Business -> Data Access -> Entities)", issue.Description);
        Assert.EndsWith($"{from}.csproj", issue.FilePath);
    }

    // The host wires up data access for dependency injection; only code using it (ARCH001) is a problem.
    [Fact]
    public async Task ARCH002_AllowsPresentationToReferenceDataAccess()
    {
        Project("Shop.Entities");
        Project("Shop.DAL", ["Shop.Entities"]);
        Project("Shop.BLL", ["Shop.DAL"]);
        var web = Project("Shop.Web", ["Shop.BLL", "Shop.DAL"]);

        Assert.Null(await new ARCH002LayerDependencyReversed().EvaluateAsync(Context(web)));
    }

    [Fact]
    public async Task ARCH002_ChecksNothing_WhenTheArchitectureIsNotRecognised()
    {
        Project("Shop.Worker", ["Shop.Api"]);
        var api = Project("Shop.Api", ["Shop.Worker"]);

        Assert.Null(await new ARCH002LayerDependencyReversed().EvaluateAsync(Context(api)));
    }

    // Clean names mean nothing under a configured layered style, and the other way round.
    [Fact]
    public async Task ARCH002_UsesTheConfiguredStyle()
    {
        Project("Shop.Application", ["Shop.Infrastructure"]);
        var infrastructure = Project("Shop.Infrastructure", ["Shop.Application"]);

        Assert.NotNull(await new ARCH002LayerDependencyReversed().EvaluateAsync(Context(infrastructure)));
        Assert.Null(await new ARCH002LayerDependencyReversed().EvaluateAsync(Context(infrastructure, ArchitectureStyle.Layered)));
    }

    // doctor passes the configured style through ProjectAnalyzer; if it were dropped, rules would fall
    // back to detection and quietly check the wrong architecture.
    [Fact]
    public async Task ProjectAnalyzer_GivesRulesTheConfiguredStyle()
    {
        Project("Shop.Application", ["Shop.Infrastructure"]);
        var infrastructure = Project("Shop.Infrastructure", ["Shop.Application"]);
        IAnalysisRule[] rules = [new ARCH002LayerDependencyReversed()];

        var detected = await new ProjectAnalyzer(rules).AnalyzeAsync(infrastructure);
        var configured = await new ProjectAnalyzer(rules, architecture: ArchitectureStyle.Layered).AnalyzeAsync(infrastructure);

        Assert.Single(detected.Issues);
        Assert.Empty(configured.Issues);
    }

    // ---- ARCH003 under layered

    [Fact]
    public async Task ARCH003_FlagsInfrastructurePackagesInBusinessAndEntities()
    {
        Project("Shop.Entities", packages: """<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />""");
        Project("Shop.DAL", ["Shop.Entities"], """<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />""");
        Project("Shop.BLL", ["Shop.DAL"], """<PackageReference Include="StackExchange.Redis" Version="2.8.0" />""");
        var web = Project("Shop.Web", ["Shop.BLL"]);

        var issue = await new ARCH003InnerLayerFrameworkDependency().EvaluateAsync(Context(web));

        Assert.NotNull(issue);
        Assert.Contains("Shop.Entities (Entities) references Microsoft.EntityFrameworkCore", issue!.Description);
        Assert.Contains("Shop.BLL (Business) references StackExchange.Redis", issue.Description);
        Assert.DoesNotContain("Shop.DAL", issue.Description);
        Assert.Contains("Entities and Business should only describe what the system does", issue.Description);
        Assert.Contains("move the code that needs these packages into Data Access", issue.Description);
        Assert.Contains("behind an interface declared in Business", issue.Description);
    }

    [Fact]
    public async Task ARCH003_AllowsPlainEfCoreInBusiness_LikeApplication()
    {
        Project("Shop.DAL");
        Project("Shop.BLL", ["Shop.DAL"], """<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />""");
        var web = Project("Shop.Web", ["Shop.BLL"]);

        Assert.Null(await new ARCH003InnerLayerFrameworkDependency().EvaluateAsync(Context(web)));
    }

    [Fact]
    public async Task ARCH003_ChecksNothing_WhenTheArchitectureIsNotRecognised()
    {
        var api = Project("Shop.Api", packages: """<PackageReference Include="Npgsql" Version="10.0.0" />""");

        Assert.Null(await new ARCH003InnerLayerFrameworkDependency().EvaluateAsync(Context(api)));
    }

    // ---- ARCH001 names the project's own layers

    [Theory]
    [InlineData(ArchitectureStyle.Layered, "through its Data Access reference", "Inject a Business service")]
    [InlineData(ArchitectureStyle.Clean, "through its Infrastructure reference", "Inject an Application service")]
    public async Task ARCH001_AdviceUsesTheArchitecturesLayerNames(ArchitectureStyle style, string reference, string advice)
    {
        Project("Shop.DAL");
        var web = Project("Shop.Web", ["Shop.DAL"]);
        var directory = Path.GetDirectoryName(web)!;
        File.WriteAllText(Path.Combine(directory, "Program.cs"), "var builder = WebApplication.CreateBuilder(args);");
        File.WriteAllText(Path.Combine(directory, "OrdersController.cs"), "public class OrdersController(ShopDbContext db) { }");

        var issue = await new ARCH001ApiDirectlyAccessesPersistenceLayer().EvaluateAsync(
            ProjectContextBuilder.Build(web) with { Architecture = style });

        Assert.Contains(reference, issue!.Description);
        Assert.Contains(advice, issue.Description);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string LayeredSolution((string From, string To)? extra = null)
    {
        string[] With(string name, params string[] references) =>
            extra is { } e && e.From == name ? [.. references, e.To] : references;

        Project("Shop.Entities", With("Shop.Entities"));
        Project("Shop.DAL", With("Shop.DAL", "Shop.Entities"));
        Project("Shop.BLL", With("Shop.BLL", "Shop.DAL", "Shop.Entities"));
        return Project("Shop.Web", With("Shop.Web", "Shop.BLL", "Shop.Entities"));
    }

    // <_root>/<name>/<name>.csproj, referencing siblings with the backslashes `dotnet add reference` writes.
    private string Project(string name, string[]? references = null, string packages = "")
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"{name}.csproj");

        var projectReferences = string.Join("\n", (references ?? []).Select(r => $"""    <ProjectReference Include="..\{r}\{r}.csproj" />"""));
        File.WriteAllText(file, $"<Project>\n  <ItemGroup>\n{packages}\n{projectReferences}\n  </ItemGroup>\n</Project>\n");
        return file;
    }

    private static ProjectContext Context(string projectFile, ArchitectureStyle? architecture = null) => new()
    {
        RootDirectory = Path.GetDirectoryName(projectFile)!,
        ProjectFile = projectFile,
        ProjectName = Path.GetFileNameWithoutExtension(projectFile),
        SourceFiles = [],
        ProjectReferences = [],
        PackageReferences = [],
        Architecture = architecture,
    };
}
