using AspireForge.Cli.Output;

namespace AspireForge.Cli.Tests.Output;

public class UnifiedDiffTests
{
    private static string Numbered(int count, params (int After, string Text)[] inserts)
    {
        var lines = Enumerable.Range(1, count).Select(i => $"line {i}").ToList();

        foreach (var (after, text) in inserts.OrderByDescending(insert => insert.After))
        {
            lines.Insert(after, text);
        }

        return string.Join('\n', lines) + "\n";
    }

    [Fact]
    public void IsEmpty_WhenNothingChanged() =>
        Assert.Empty(UnifiedDiff.Build("a.cs", "same\n", "same\n"));

    [Fact]
    public void IsEmpty_WhenOnlyLineEndingsDiffer() =>
        Assert.Empty(UnifiedDiff.Build("a.cs", "a\r\nb\r\n", "a\nb\n"));

    [Fact]
    public void ShowsAnAddedLine_WithThreeLinesOfContext()
    {
        var diff = UnifiedDiff.Build("a.cs", Numbered(10), Numbered(10, (5, "new")));

        Assert.Equal(
        [
            "--- a.cs",
            "+++ a.cs",
            "@@ -3,6 +3,7 @@",
            " line 3",
            " line 4",
            " line 5",
            "+new",
            " line 6",
            " line 7",
            " line 8",
        ],
        diff);
    }

    [Fact]
    public void ShowsARemovedLine()
    {
        var diff = UnifiedDiff.Build("a.cs", "a\nb\nc\n", "a\nc\n");

        Assert.Equal(["--- a.cs", "+++ a.cs", "@@ -1,3 +1,2 @@", " a", "-b", " c"], diff);
    }

    [Fact]
    public void ShowsAChangedLine_AsARemovalAndAnAddition()
    {
        var diff = UnifiedDiff.Build("a.cs", "a\nb\nc\n", "a\nB\nc\n");

        Assert.Contains("-b", diff);
        Assert.Contains("+B", diff);
    }

    [Fact]
    public void HandlesAChangeAtTheStartAndEnd()
    {
        Assert.Equal(["--- a.cs", "+++ a.cs", "@@ -1,1 +1,2 @@", "+new", " a"], UnifiedDiff.Build("a.cs", "a\n", "new\na\n"));
        Assert.Equal(["--- a.cs", "+++ a.cs", "@@ -1,1 +1,2 @@", " a", "+new"], UnifiedDiff.Build("a.cs", "a\n", "a\nnew\n"));
    }

    [Fact]
    public void SplitsDistantChanges_IntoSeparateHunks()
    {
        var diff = UnifiedDiff.Build("a.cs", Numbered(30), Numbered(30, (5, "first"), (25, "second")));

        Assert.Equal(2, diff.Count(line => line.StartsWith("@@", StringComparison.Ordinal)));
        Assert.Contains("@@ -3,6 +3,7 @@", diff);
        Assert.Contains("@@ -23,6 +24,7 @@", diff);
        Assert.DoesNotContain(" line 15", diff);
    }

    [Fact]
    public void JoinsNearbyChanges_IntoOneHunk()
    {
        var diff = UnifiedDiff.Build("a.cs", Numbered(30), Numbered(30, (10, "first"), (14, "second")));

        Assert.Single(diff, line => line.StartsWith("@@", StringComparison.Ordinal));
        Assert.Contains("@@ -8,10 +8,12 @@", diff);
    }

    // Real fixes insert the same blank line in several places, the case that confuses a naive compare.
    [Fact]
    public void ShowsOnlyTheInsertedLines_WhenTheFileHasRepeatedLines()
    {
        var diff = UnifiedDiff.Build("a.cs", "a\n\nb\n\nc\n", "a\n\nnew\n\nb\n\nc\n");

        Assert.Equal(2, diff.Count(line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal)));
        Assert.DoesNotContain(diff, line => line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal));
    }

    [Fact]
    public void StillWorks_OnFilesTooLargeToCompareLineByLine()
    {
        var before = string.Join('\n', Enumerable.Range(0, 3000).Select(i => $"old {i}"));
        var after = string.Join('\n', Enumerable.Range(0, 3000).Select(i => $"new {i}"));

        var diff = UnifiedDiff.Build("a.cs", before, after);

        Assert.Equal(3000, diff.Count(line => line.StartsWith("-old", StringComparison.Ordinal)));
        Assert.Equal(3000, diff.Count(line => line.StartsWith("+new", StringComparison.Ordinal)));
    }
}
