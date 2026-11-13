using System.Text;
using AspireForge.Core.Text;

namespace AspireForge.Core.Tests.Text;

public class TextFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_root, "Program.cs");

    public TextFileTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("a\r\nb\r\n", "a\r\nadded\r\nb\r\n")]
    [InlineData("a\nb\n", "a\nadded\nb\n")]
    [InlineData("a\nb", "a\nadded\nb")]
    public void Edit_KeepsLineEndingsAndTheFinalNewline(string before, string after)
    {
        File.WriteAllText(FilePath, before);

        TextFile.Edit(FilePath, lines => lines.Insert(1, "added"));

        Assert.Equal(after, File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Edit_KeepsTheByteOrderMark(bool hasBom)
    {
        File.WriteAllText(FilePath, "a\n", new UTF8Encoding(hasBom));

        TextFile.Edit(FilePath, lines => lines.Insert(1, "added"));

        Assert.Equal(hasBom, File.ReadAllBytes(FilePath).AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    [Fact]
    public void Edit_DoesNotWrite_WhenNothingChanged()
    {
        File.WriteAllText(FilePath, "a\n");
        var written = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(FilePath, written);

        TextFile.Edit(FilePath, _ => { });

        Assert.Equal(written, File.GetLastWriteTimeUtc(FilePath));
    }

    [Fact]
    public void Read_SeparatesTheByteOrderMarkFromTheText()
    {
        File.WriteAllText(FilePath, "a", new UTF8Encoding(true));

        var content = TextFile.Read(FilePath);

        Assert.True(content.HasBom);
        Assert.Equal("a", content.Text);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
