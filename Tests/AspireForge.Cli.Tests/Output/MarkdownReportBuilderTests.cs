using AspireForge.Analyzers;
using AspireForge.Analyzers.Suppressions;
using AspireForge.Cli.Output;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Tests.Output;

public class MarkdownReportBuilderTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "orders");
    private static readonly string ProjectFile = Path.Combine(Root, "Orders.Api.csproj");

    private static readonly ProjectContext Context = new()
    {
        RootDirectory = Root,
        ProjectFile = ProjectFile,
        ProjectName = "Orders.Api",
        SourceFiles = [],
        ProjectReferences = [],
        PackageReferences = [],
    };

    private static string Build(
        IReadOnlyCollection<AnalysisIssue>? issues = null,
        IReadOnlyList<AcceptedFinding>? accepted = null,
        int unmatched = 0) =>
        MarkdownReportBuilder.Build(
            Context,
            new AnalysisResult("Orders.Api", issues ?? []),
            AnalysisRuleSet.CreateDefault(),
            accepted ?? [],
            unmatched,
            new HashSet<string>(),
            Root);

    [Fact]
    public void ShowsTheScoreAndTotals()
    {
        var report = Build([new AnalysisIssue("SEC002", "CORS", "Bad", Severity.Error, Path.Combine(Root, "Program.cs"), 4)]);

        Assert.Contains("## AspireForge Doctor: Orders.Api", report);
        Assert.Contains("**Production Readiness: 6/10 (60/100)** - capped at 60", report);
        Assert.Contains("| 1 | 0 | 0 | 39 |", report);
    }

    [Fact]
    public void SaysSo_WhenThereAreNoFindings()
    {
        var report = Build();

        Assert.Contains("10/10 (100/100)", report);
        Assert.Contains("No findings.", report);
        Assert.DoesNotContain("### Findings", report);
    }

    [Fact]
    public void ListsFindings_MostSevereFirst_WithLocationAndDocsLink()
    {
        var report = Build(
        [
            new AnalysisIssue("OBS005", "Console output", "Why", Severity.Suggestion, Path.Combine(Root, "Program.cs"), 7),
            new AnalysisIssue("SEC002", "CORS policy allows unrestricted origins", "Why", Severity.Error, Path.Combine(Root, "Program.cs"), 4),
        ]);

        Assert.Contains("### Findings (2)", report);
        Assert.Contains("| error | [SEC002](https://github.com/", report);
        Assert.Contains("docs/rules/security.md#sec002---cors-policy-allows-unrestricted-origins)", report);
        Assert.Contains("| `Program.cs:4` |", report);
        Assert.True(report.IndexOf("| error |", StringComparison.Ordinal) < report.IndexOf("| suggestion |", StringComparison.Ordinal));
    }

    [Fact]
    public void ShowsWholeProject_ForFindingsAboutTheProject()
    {
        var report = Build([new AnalysisIssue("SEC001", "Authentication not configured", "Why", Severity.Warning, ProjectFile)]);

        Assert.Contains("| whole project |", report);
        Assert.DoesNotContain("Orders.Api.csproj", report);
    }

    [Fact]
    public void FoldsTheDetail_AndGivesTheFixCommand()
    {
        var report = Build([new AnalysisIssue("SEC001", "Authentication not configured", "No call was found.", Severity.Warning, ProjectFile)]);

        var details = report[report.IndexOf("<details>", StringComparison.Ordinal)..report.IndexOf("</details>", StringComparison.Ordinal)];

        Assert.Contains("No call was found.", details);
        Assert.Contains("Fix: `aspireforge add authentication`", details);
    }

    // A description quoting code mustn't break the table or be read as HTML or emphasis.
    [Fact]
    public void EscapesMarkdownInFindings()
    {
        var report = Build(
        [
            new AnalysisIssue("SEC002", "a|b", "WithOrigins(\"*\") in List<string>\nnext", Severity.Error, Path.Combine(Root, "Program.cs"), 4),
        ]);

        Assert.Contains("a\\|b", report);
        Assert.Contains("WithOrigins(\"\\*\") in List&lt;string> next", report);
    }

    [Fact]
    public void ListsWhatWasAccepted_AndDoesNotCountItAsPassed()
    {
        var report = Build(accepted:
        [
            new AcceptedFinding("SEC002", Path.Combine(Root, "Program.cs"), 4, AcceptedBy.InlineComment, "public | API"),
            new AcceptedFinding("OBS005", Path.Combine(Root, "Program.cs"), 9, AcceptedBy.Baseline),
            new AcceptedFinding("SEC001", null, null, AcceptedBy.Baseline),
        ]);

        Assert.Contains("> Not counted in the score: 1 suppressed inline, 2 baselined.", report);
        Assert.Contains("### Accepted (3)", report);
        Assert.Contains("| SEC002 | `Program.cs:4` | suppressed: public \\| API |", report);
        Assert.Contains("| OBS005 | `Program.cs:9` | baselined |", report);
        Assert.Contains("| SEC001 | whole project | baselined |", report);
        Assert.Contains("| 0 | 0 | 0 | 37 |", report);
    }

    [Theory]
    [InlineData(1, "1 baseline entry no longer matches anything.")]
    [InlineData(2, "2 baseline entries no longer match anything.")]
    public void SaysWhenBaselineEntriesAreOutOfDate(int unmatched, string expected) =>
        Assert.Contains(expected, Build(unmatched: unmatched));
}
