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
