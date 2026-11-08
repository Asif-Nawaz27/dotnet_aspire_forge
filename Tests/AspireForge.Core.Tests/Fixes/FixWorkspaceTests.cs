using System.Text;
using AspireForge.Core.Features;
using AspireForge.Core.Fixes;

namespace AspireForge.Core.Tests.Fixes;

public class FixWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_root, "Program.cs");

    public FixWorkspaceTests() => Directory.CreateDirectory(_root);

    private FixWorkspace Workspace() => new(new FeatureContext { ProjectName = "Sample", RootPath = _root });

    private static void InsertLine(List<string> lines) => lines.Insert(1, "added");

    [Fact]
    public void Edit_DoesNotTouchTheDisk_UntilCommit()
    {
        File.WriteAllText(FilePath, "a\nb\n");
        var workspace = Workspace();

        workspace.Edit(FilePath, InsertLine);

        Assert.Equal("a\nb\n", File.ReadAllText(FilePath));

        workspace.Commit();

        Assert.Equal("a\nadded\nb\n", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Changes_ShowsTheFileBeforeAndAfter()
    {
        File.WriteAllText(FilePath, "a\nb\n");
        var workspace = Workspace();

        workspace.Edit(FilePath, InsertLine);

        var change = Assert.Single(workspace.Changes);
        Assert.Equal(FilePath, change.Path);
        Assert.Equal("a\nb\n", change.Before);
        Assert.Equal("a\nadded\nb\n", change.After);
    }

    [Fact]
    public void Changes_LeavesOutFilesThatEndedUpTheSame()
    {
        File.WriteAllText(FilePath, "a\nb\n");
        var workspace = Workspace();

        workspace.Edit(FilePath, _ => { });

        Assert.Empty(workspace.Changes);
    }

    // Several fixes edit Program.cs, and each has to see what the one before it did.
    [Fact]
    public void Edits_BuildOnEachOther()
    {
        File.WriteAllText(FilePath, "a\n");
        var workspace = Workspace();

        workspace.Edit(FilePath, lines => lines.Insert(1, "b"));
        workspace.Edit(FilePath, lines => lines.Insert(2, "c"));

        Assert.Equal("a\nb\nc\n", Assert.Single(workspace.Changes).After);
    }

    [Theory]
    [InlineData("a\r\nb\r\n", "a\r\nadded\r\nb\r\n")]
    [InlineData("a\nb\n", "a\nadded\nb\n")]
    [InlineData("a\nb", "a\nadded\nb")]
    public void Edit_KeepsLineEndingsAndTheFinalNewline(string before, string after)
    {
        File.WriteAllText(FilePath, before);
        var workspace = Workspace();

        workspace.Edit(FilePath, InsertLine);
        workspace.Commit();

        Assert.Equal(after, File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Commit_KeepsTheByteOrderMark(bool hasBom)
    {
        File.WriteAllText(FilePath, "a\nb\n", new UTF8Encoding(hasBom));
        var workspace = Workspace();

        workspace.Edit(FilePath, InsertLine);
        workspace.Commit();

        Assert.Equal(hasBom, File.ReadAllBytes(FilePath).AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    [Fact]
    public void Edit_ThatThrows_LeavesTheFileAsItWas()
    {
        File.WriteAllText(FilePath, "a\n");
        var workspace = Workspace();

        Assert.Throws<InvalidOperationException>(() => workspace.Edit(FilePath, lines =>
        {
            lines.Add("half done");
            throw new InvalidOperationException();
        }));

        Assert.Empty(workspace.Changes);
    }

    [Fact]
    public void Commit_LeavesUnchangedFilesAlone()
    {
        File.WriteAllText(FilePath, "a\n");
        var written = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(FilePath, written);
        var workspace = Workspace();

        workspace.Edit(FilePath, _ => { });
        workspace.Commit();

        Assert.Equal(written, File.GetLastWriteTimeUtc(FilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
