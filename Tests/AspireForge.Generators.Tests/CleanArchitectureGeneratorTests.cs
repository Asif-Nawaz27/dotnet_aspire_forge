using AspireForge.Core.Generation;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Tests;

public class CleanArchitectureGeneratorTests : IDisposable
{
    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
    private readonly CleanArchitectureGenerator _generator = new();

    [Fact]
    public void Generate_CreatesTheFullCleanArchitectureLayout()
    {
        var result = _generator.Generate(new GenerationOptions
        {
            ProjectName = "SampleApi",
            Architecture = "clean",
            OutputPath = _outputPath,
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

        var root = Path.Combine(_outputPath, "SampleApi");
        Assert.True(File.Exists(Path.Combine(root, "src", "SampleApi.Domain", "SampleApi.Domain.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "src", "SampleApi.Application", "SampleApi.Application.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "src", "SampleApi.Infrastructure", "SampleApi.Infrastructure.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "src", "SampleApi.Api", "SampleApi.Api.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "SampleApi.Api.IntegrationTests", "SampleApi.Api.IntegrationTests.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "Dockerfile")));
        Assert.True(File.Exists(Path.Combine(root, ".dockerignore")));
        Assert.True(File.Exists(Path.Combine(root, "docker-compose.yml")));
        Assert.True(File.Exists(Path.Combine(root, ".gitignore")));
        Assert.True(File.Exists(Path.Combine(root, "README.md")));
        Assert.True(File.Exists(Path.Combine(root, ".github", "workflows", "ci.yml")));
        Assert.True(Directory.EnumerateFiles(root, "*.sln*", SearchOption.TopDirectoryOnly).Any());
    }

    // Regression test: with a newer SDK installed (the .NET 11 SDK on CI runners), templates default to
    // its framework, and projects came out as net11.0 next to a Dockerfile and CI workflow on 10.0. Every
    // generated project, the Dockerfile, and the workflow have to agree on GeneratedDotNet.
    [Fact]
    public void Generate_TargetsOneDotNetVersion_InEveryProjectTheDockerfileAndCi()
    {
        var result = _generator.Generate(new GenerationOptions
        {
            ProjectName = "SampleApi",
            Architecture = "clean",
            OutputPath = _outputPath,
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

        var root = Path.Combine(_outputPath, "SampleApi");
        var projects = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).ToList();

        Assert.Equal(6, projects.Count); // Domain, Application, Infrastructure, Api, ServiceDefaults, IntegrationTests
        Assert.All(projects, project => Assert.Equal(
            GeneratedDotNet.TargetFramework, FrameworkOf(project)));

        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        Assert.Contains($"dotnet/sdk:{GeneratedDotNet.Channel} AS build", dockerfile, StringComparison.Ordinal);
        Assert.Contains($"dotnet/aspnet:{GeneratedDotNet.Channel} AS runtime", dockerfile, StringComparison.Ordinal);

        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        Assert.Contains($"dotnet-version: '{GeneratedDotNet.Channel}.x'", workflow, StringComparison.Ordinal);
    }

    // Passed to the template rather than left to its default, which follows the newest installed SDK.
    [Theory]
    [InlineData("classlib")]
    [InlineData("webapi")]
    [InlineData("xunit")]
    public void EveryTemplate_IsAskedForTheGeneratedFramework(string templateName)
    {
        var arguments = CleanArchitectureGenerator.ConfigureTemplateArguments(
            new ProjectTemplate("Layer", templateName, "SampleApi.Layer", "out"),
            new GenerationOptions { ProjectName = "SampleApi", Architecture = "clean", OutputPath = "." });

        var framework = arguments.IndexOf("--framework");
        Assert.True(framework >= 0, string.Join(' ', arguments));
        Assert.Equal(GeneratedDotNet.TargetFramework, arguments[framework + 1]);
    }

    private static string? FrameworkOf(string csproj) =>
        System.Xml.Linq.XDocument.Load(csproj).Descendants("TargetFramework").FirstOrDefault()?.Value;

    [Fact]
    public void Generate_WiresProjectReferences_MatchingCleanArchitectureLayering()
    {
        var result = _generator.Generate(new GenerationOptions
        {
            ProjectName = "SampleApi",
            Architecture = "clean",
            OutputPath = _outputPath,
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

        var root = Path.Combine(_outputPath, "SampleApi");
        var applicationCsproj = File.ReadAllText(Path.Combine(root, "src", "SampleApi.Application", "SampleApi.Application.csproj"));
        var infrastructureCsproj = File.ReadAllText(Path.Combine(root, "src", "SampleApi.Infrastructure", "SampleApi.Infrastructure.csproj"));
        var apiCsproj = File.ReadAllText(Path.Combine(root, "src", "SampleApi.Api", "SampleApi.Api.csproj"));

        Assert.Contains("SampleApi.Domain.csproj", applicationCsproj);
        Assert.Contains("SampleApi.Domain.csproj", infrastructureCsproj);
        Assert.Contains("SampleApi.Application.csproj", infrastructureCsproj);
        Assert.Contains("SampleApi.Application.csproj", apiCsproj);
        Assert.Contains("SampleApi.Infrastructure.csproj", apiCsproj);
    }

    [Fact]
    public void Generate_Fails_WhenTargetDirectoryAlreadyExistsAndOverwriteIsNotSet()
    {
        var options = new GenerationOptions { ProjectName = "SampleApi", Architecture = "clean", OutputPath = _outputPath };
        Directory.CreateDirectory(Path.Combine(_outputPath, "SampleApi"));

        var result = _generator.Generate(options);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }
    }
}
