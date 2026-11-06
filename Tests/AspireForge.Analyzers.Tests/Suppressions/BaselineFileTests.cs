using AspireForge.Analyzers.Suppressions;

namespace AspireForge.Analyzers.Tests.Suppressions;

public class BaselineFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    public BaselineFileTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Load_ReturnsNull_WhenThereIsNoBaseline() =>
        Assert.Null(BaselineFile.Load(_root));

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var entry = new BaselineEntry("SEC002", "src/Program.cs", "abc123", 2);

        new BaselineFile(BaselineFile.CurrentVersion, [entry]).Save(_root);

        var loaded = BaselineFile.Load(_root);

        Assert.Equal(BaselineFile.CurrentVersion, loaded!.Version);
        Assert.Equal(entry, Assert.Single(loaded.Findings));
    }

    // Regenerating an unchanged baseline must not produce a diff.
    [Fact]
    public void Save_SortsEntries_SoTheFileIsStable()
    {
        var first = new BaselineEntry("OBS005", "a.cs", "1", 1);
        var second = new BaselineEntry("SEC002", "a.cs", "1", 1);
        var third = new BaselineEntry("SEC002", "b.cs", "1", 1);

        new BaselineFile(BaselineFile.CurrentVersion, [third, first, second]).Save(_root);
        var once = File.ReadAllText(BaselineFile.PathFor(_root));

        new BaselineFile(BaselineFile.CurrentVersion, [second, third, first]).Save(_root);

        Assert.Equal(once, File.ReadAllText(BaselineFile.PathFor(_root)));
        Assert.Equal([first, second, third], BaselineFile.Load(_root)!.Findings);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"version\": 1 }")]
    public void Load_RefusesAFileItCantUse(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BaselineFile.PathFor(_root))!);
        File.WriteAllText(BaselineFile.PathFor(_root), content);

        Assert.Throws<InvalidDataException>(() => BaselineFile.Load(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
