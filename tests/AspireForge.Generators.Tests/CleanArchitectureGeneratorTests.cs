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
        Assert.True(File.Exists(Path.Combine(root, "tests", "SampleApi.IntegrationTests", "SampleApi.IntegrationTests.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "Dockerfile")));
        Assert.True(File.Exists(Path.Combine(root, ".dockerignore")));
        Assert.True(File.Exists(Path.Combine(root, "docker-compose.yml")));
        Assert.True(File.Exists(Path.Combine(root, ".gitignore")));
        Assert.True(File.Exists(Path.Combine(root, "README.md")));
        Assert.True(File.Exists(Path.Combine(root, ".github", "workflows", "ci.yml")));
        Assert.True(Directory.EnumerateFiles(root, "*.sln*", SearchOption.TopDirectoryOnly).Any());
    }

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
