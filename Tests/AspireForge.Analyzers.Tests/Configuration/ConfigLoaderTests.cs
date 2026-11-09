using AspireForge.Analyzers.Configuration;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Configuration;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_ReturnsEmptyConfig_WhenNoConfigFileExistsAnywhereUpward()
    {
        var projectDirectory = Path.Combine(_root, "src", "Api");
        Directory.CreateDirectory(projectDirectory);

        var config = ConfigLoader.Load(projectDirectory);

        Assert.Empty(config.Rules);
    }

    [Theory]
    [InlineData("layered", AspireForge.Core.Configuration.ArchitectureStyle.Layered)]
    [InlineData("Clean", AspireForge.Core.Configuration.ArchitectureStyle.Clean)]
    public void Load_ReadsTheArchitecture(string value, AspireForge.Core.Configuration.ArchitectureStyle expected)
    {
        WriteConfig($$"""{ "architecture": "{{value}}" }""");

        Assert.Equal(expected, ConfigLoader.Load(_root).Architecture);
    }

    [Fact]
    public void Load_LeavesTheArchitectureToDetection_WhenItIsNotSet()
    {
        WriteConfig("""{ "rules": {} }""");

        Assert.Null(ConfigLoader.Load(_root).Architecture);
    }

    // Used to escape as a JsonException with a stack trace.
    [Fact]
    public void Load_ExplainsAnUnsupportedArchitecture()
    {
        WriteConfig("""{ "architecture": "microservices" }""");

        var exception = Assert.Throws<InvalidDataException>(() => ConfigLoader.Load(_root));

        Assert.Contains("has an unsupported \"architecture\". It takes \"clean\" or \"layered\"", exception.Message);
        Assert.DoesNotContain("System.", exception.Message);
    }

    [Fact]
    public void Load_SaysWhereTheJsonIsBroken()
    {
        WriteConfig("{\n  \"rules\": {\n    \"SEC001\": \n}");

        var exception = Assert.Throws<InvalidDataException>(() => ConfigLoader.Load(_root));

        Assert.Contains("isn't valid JSON on line 4.", exception.Message);
    }

    private void WriteConfig(string json)
    {
        Directory.CreateDirectory(Path.Combine(_root, ".aspireforge"));
        File.WriteAllText(Path.Combine(_root, ".aspireforge", "config.json"), json);
    }

    [Fact]
    public void Load_FindsConfigFile_InAnAncestorDirectory()
    {
        var projectDirectory = Path.Combine(_root, "src", "Api");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(Path.Combine(_root, ".aspireforge"));
        File.WriteAllText(
            Path.Combine(_root, ".aspireforge", "config.json"),
            """{ "rules": { "SEC001": { "severity": "error" }, "TEST002": { "enabled": false } } }""");

        var config = ConfigLoader.Load(projectDirectory);

        Assert.Equal(Severity.Error, config.Rules["SEC001"].Severity);
        Assert.False(config.Rules["TEST002"].Enabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
