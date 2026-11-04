using AspireForge.Cli.Output;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Tests.Output;

public class FindingFormatterTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "aspireforge-format");
    private static readonly string ProjectFile = Path.Combine(Root, "src", "Api", "Api.csproj");

    [Fact]
    public void Format_ShowsWhereWhyFixAndDocs_ForALineLevelFinding()
    {
        var issue = new AnalysisIssue(
            "SEC006", "HSTS missing", "UseHsts() is only called inside a development-only block.", Severity.Warning,
            Path.Combine(Root, "src", "Api", "Program.cs"), 38);

        var lines = FindingFormatter.Format(issue, "https://docs/sec006", "aspireforge fix SEC006", ProjectFile, Root, 100).ToList();

        Assert.Equal(
            [
                "⚠ SEC006 · HSTS missing · warning",
                "  Where  src/Api/Program.cs:38",
                "  Why    UseHsts() is only called inside a development-only block.",
                "  Fix    aspireforge fix SEC006",
                "  Docs   https://docs/sec006",
            ],
            lines);
    }

    [Fact]
    public void Format_ShowsWholeProjectScope_ForAFindingWithNoSpecificLocation()
    {
        var issue = new AnalysisIssue("REL002", "Global exception handling missing", "No handler.", Severity.Warning, ProjectFile);

        var lines = FindingFormatter.Format(issue, null, null, ProjectFile, Root, 100).ToList();

        Assert.Contains("  Scope  whole project", lines);
        Assert.DoesNotContain(lines, line => line.Contains("Where"));
    }

    [Fact]
    public void Format_PointsAtTheProjectFile_WhenTheFindingHasALineInIt()
    {
        var issue = new AnalysisIssue("DEP004", "Prerelease", "Preview package.", Severity.Warning, ProjectFile, 12);

        var lines = FindingFormatter.Format(issue, null, null, ProjectFile, Root, 100).ToList();

        Assert.Contains("  Where  src/Api/Api.csproj:12", lines);
    }

    [Fact]
    public void Format_OmitsFixAndDocs_WhenThereAreNone()
    {
        var issue = new AnalysisIssue("X001", "Title", "Text.", Severity.Error, ProjectFile);

        var lines = FindingFormatter.Format(issue, null, null, ProjectFile, Root, 100).ToList();

        Assert.DoesNotContain(lines, line => line.Contains("Fix") || line.Contains("Docs"));
        Assert.StartsWith("✗ X001", lines[0]);
    }

    [Fact]
    public void Format_WrapsLongExplanations_UnderTheWhyLabel()
    {
        var issue = new AnalysisIssue(
            "X001", "Title", string.Join(' ', Enumerable.Repeat("word", 40)), Severity.Warning, ProjectFile);

        var why = FindingFormatter.Format(issue, null, null, ProjectFile, Root, 60)
            .SkipWhile(line => !line.StartsWith("  Why")).TakeWhile(line => !line.StartsWith("  Docs")).ToList();

        Assert.True(why.Count > 1);
        Assert.All(why, line => Assert.True(line.Length <= 60, $"'{line}' is longer than 60"));
        Assert.All(why.Skip(1), line => Assert.StartsWith(new string(' ', 9), line));
    }

    [Theory]
    [InlineData(Severity.Critical, "✗")]
    [InlineData(Severity.Error, "✗")]
    [InlineData(Severity.Warning, "⚠")]
    [InlineData(Severity.Suggestion, "○")]
    [InlineData(Severity.Info, "○")]
    public void Symbol_DistinguishesErrorsWarningsAndSuggestions(Severity severity, string symbol) =>
        Assert.Equal(symbol, FindingFormatter.Symbol(severity));

    [Fact]
    public void DisplayPath_IsAbsolute_OutsideTheCurrentDirectory()
    {
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "Program.cs");

        Assert.Equal(outside, FindingFormatter.DisplayPath(outside, Root));
    }

    [Fact]
    public void Wrap_KeepsAnOverlongWordOnItsOwnLine()
    {
        var url = "https://example.com/" + new string('x', 80);

        Assert.Equal(["see", url, "for details"], FindingFormatter.Wrap($"see {url} for details", 20));
    }
}
