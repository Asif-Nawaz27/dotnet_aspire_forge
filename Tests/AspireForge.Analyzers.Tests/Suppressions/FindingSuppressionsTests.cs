using System.Text.RegularExpressions;
using AspireForge.Analyzers.Rules.Deployment;
using AspireForge.Analyzers.Rules.Performance;
using AspireForge.Analyzers.Rules.Reliability;
using AspireForge.Analyzers.Rules.Security;
using AspireForge.Analyzers.Suppressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Suppressions;

public class FindingSuppressionsTests
{
    private const string Bad = "policy.AllowAnyOrigin();";

    private readonly SEC002CorsPolicyAllowsUnrestrictedOrigins _rule = new();

    [Fact]
    public async Task InlineComment_HidesOnlyTheOccurrenceItsOn()
    {
        using var project = TestProject.Create($"""
            // aspireforge-disable-next-line SEC002: public read-only API
            {Bad}
            other.AllowAnyOrigin();
            """);

        var issue = await Evaluate(_rule, project);

        Assert.NotNull(issue);
        Assert.Equal(3, issue!.LineNumber);
        Assert.Contains("Program.cs:3", issue.Description);
        Assert.DoesNotContain("Program.cs:2", issue.Description);
    }

    [Fact]
    public async Task InlineComment_OnTheWorstOccurrence_LeavesTheSeverityOfWhatRemains()
    {
        using var project = TestProject.Create("""
            policy.AllowAnyOrigin(); // aspireforge-disable-line SEC002
            policy.SetIsOriginAllowed(o => o.Contains("example.com"));
            """);

        var issue = await Evaluate(_rule, project);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Equal(2, issue.LineNumber);
    }

    [Fact]
    public async Task InlineComment_OnEveryOccurrence_RemovesTheFinding()
    {
        using var project = TestProject.Create($"{Bad} // aspireforge-disable-line SEC002");
        var suppressions = FindingSuppressions.Create(project.Context.RootDirectory, baseline: null);

        Assert.Null(await Evaluate(_rule, project, suppressions));

        var accepted = Assert.Single(suppressions.Accepted);
        Assert.Equal(AcceptedBy.InlineComment, accepted.By);
        Assert.Equal(1, accepted.Line);
    }

    [Fact]
    public async Task InlineComment_ForAnotherRule_ChangesNothing()
    {
        using var project = TestProject.Create($"{Bad} // aspireforge-disable-line OBS005");

        Assert.NotNull(await Evaluate(_rule, project));
    }

    [Fact]
    public async Task InlineComment_WorksInDockerfiles()
    {
        using var project = TestProject.Create();
        File.WriteAllText(
            Path.Combine(project.Context.RootDirectory, "Dockerfile"),
            "# aspireforge-disable-next-line DEP003: base image pinned by the platform team\nFROM mcr.microsoft.com/dotnet/aspnet:latest\nUSER app\n");

        Assert.Null(await Evaluate(new DEP003ContainerProblems(), project));
    }

    [Fact]
    public async Task InlineComment_WorksInAppSettings()
    {
        using var project = TestProject.Create();
        File.WriteAllText(Path.Combine(project.Context.RootDirectory, "appsettings.json"), """
            {
              // aspireforge-disable-next-line SEC004: local emulator key, published by the vendor
              "Password": "correct-horse-battery-staple"
            }
            """);

        Assert.Null(await Evaluate(new SEC004SecretsInConfiguration(), project));
    }

    [Fact]
    public async Task InlineComment_WorksInProjectFiles()
    {
        using var project = TestProject.Create();
        File.WriteAllText(project.Context.ProjectFile, """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <!-- aspireforge-disable-next-line DEP004: no stable release yet -->
                <PackageReference Include="Some.Package" Version="1.0.0-preview.1" />
              </ItemGroup>
            </Project>
            """);

        Assert.Null(await Evaluate(new DEP004PrereleasePackages(), project));
    }

    [Fact]
    public async Task Baseline_AcceptsWhatWasThereWhenItWasRecorded()
    {
        using var project = TestProject.Create(Bad);
        var baseline = await RecordBaseline(_rule, project);
        var suppressions = FindingSuppressions.Create(project.Context.RootDirectory, baseline);

        Assert.Null(await Evaluate(_rule, project, suppressions));
        Assert.Equal(AcceptedBy.Baseline, Assert.Single(suppressions.Accepted).By);
    }

    [Fact]
    public async Task Baseline_StillMatches_WhenTheCodeMoves()
    {
        using var project = TestProject.Create(Bad);
        var baseline = await RecordBaseline(_rule, project);

        Rewrite(project, $"// a new header\n\n    {Bad}");

        Assert.Null(await Evaluate(_rule, project, FindingSuppressions.Create(project.Context.RootDirectory, baseline)));
    }

    // Git converts line endings on checkout, so a baseline written on Windows is read on Linux CI.
    [Fact]
    public async Task Baseline_StillMatches_WhenLineEndingsChange()
    {
        using var project = TestProject.Create($"first();\r\n{Bad}\r\nlast();\r\n");
        var baseline = await RecordBaseline(_rule, project);

        Rewrite(project, $"first();\n{Bad}\nlast();\n");

        Assert.Null(await Evaluate(_rule, project, FindingSuppressions.Create(project.Context.RootDirectory, baseline)));
    }

    // The whole point of a baseline: an accepted finding mustn't hide a new one behind it.
    [Fact]
    public async Task Baseline_ReportsANewOccurrence_InTheSameFile()
    {
        using var project = TestProject.Create(Bad);
        var baseline = await RecordBaseline(_rule, project);

        Rewrite(project, $"{Bad}\nother.AllowAnyOrigin();");

        var issue = await Evaluate(_rule, project, FindingSuppressions.Create(project.Context.RootDirectory, baseline));

        Assert.Equal(2, issue!.LineNumber);
        Assert.DoesNotContain("Program.cs:1", issue.Description);
    }

    [Fact]
    public async Task Baseline_ReportsAnIdenticalCopy_OfAnAcceptedLine()
    {
        using var project = TestProject.Create(Bad);
        var baseline = await RecordBaseline(_rule, project);

        Rewrite(project, $"{Bad}\n{Bad}");

        Assert.NotNull(await Evaluate(_rule, project, FindingSuppressions.Create(project.Context.RootDirectory, baseline)));
    }

    [Fact]
    public async Task Baseline_ReportsAnAcceptedLine_CopiedToAnotherFile()
    {
        using var project = TestProject.Create(Bad);
        var baseline = await RecordBaseline(_rule, project);

        var copy = Path.Combine(project.Context.RootDirectory, "Copy.cs");
        File.WriteAllText(copy, Bad);
        var context = project.Context with
        {
            SourceFiles = [.. project.Context.SourceFiles, copy],
            Suppressions = FindingSuppressions.Create(project.Context.RootDirectory, baseline),
        };

        var issue = await _rule.EvaluateAsync(context);

        Assert.Equal(copy, issue!.FilePath);
    }

    [Fact]
    public async Task Baseline_DoesNotAccept_AnInlineSuppressedOccurrence()
    {
        using var project = TestProject.Create($"{Bad} // aspireforge-disable-line SEC002");

        var baseline = await RecordBaseline(_rule, project);

        Assert.Empty(baseline.Findings);
    }

    [Fact]
    public async Task Baseline_UsesForwardSlashes_ForFilesInOtherProjects()
    {
        using var project = TestProject.Create(Bad);
        var sibling = Path.Combine(project.Context.RootDirectory, "..", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sibling);

        try
        {
            var file = Path.GetFullPath(Path.Combine(sibling, "Extensions.cs"));
            File.WriteAllText(file, Bad);
            var recorder = FindingSuppressions.Recording(project.Context.RootDirectory);

            await _rule.EvaluateAsync(project.Context with { SourceFiles = [file], Suppressions = recorder });

            var entry = Assert.Single(recorder.RecordedEntries);
            Assert.StartsWith("../", entry.File);
            Assert.DoesNotContain('\\', entry.File);
        }
        finally
        {
            Directory.Delete(sibling, recursive: true);
        }
    }

    [Fact]
    public async Task UnmatchedBaselineCount_CountsFixedFindings_OnlyForRulesThatRan()
    {
        using var project = TestProject.Create(Bad);
        var baseline = await RecordBaseline(_rule, project);

        Rewrite(project, "// fixed");

        var suppressions = FindingSuppressions.Create(project.Context.RootDirectory, baseline);
        await Evaluate(_rule, project, suppressions);

        Assert.Equal(1, suppressions.UnmatchedBaselineCount(new HashSet<string> { "SEC002" }));
        Assert.Equal(0, suppressions.UnmatchedBaselineCount(new HashSet<string> { "DEP002" }));
    }

    [Fact]
    public void ProjectFinding_IsBaselined_ByItsDescription()
    {
        using var project = TestProject.Create();
        var root = project.Context.RootDirectory;
        var issue = new AnalysisIssue("DEP002", "Vulnerable packages", "Lib 1.0.0 is vulnerable", Severity.Error, project.Context.ProjectFile);

        var recorder = FindingSuppressions.Recording(root);
        Assert.False(recorder.IsSuppressed(issue));
        var baseline = new BaselineFile(BaselineFile.CurrentVersion, recorder.RecordedEntries);

        Assert.True(FindingSuppressions.Create(root, baseline).IsSuppressed(issue));
    }

    // A baseline must not hide a newly vulnerable package behind the one it accepted.
    [Fact]
    public void ProjectFinding_ComesBack_WhenItsDescriptionChanges()
    {
        using var project = TestProject.Create();
        var root = project.Context.RootDirectory;
        var issue = new AnalysisIssue("DEP002", "Vulnerable packages", "Lib 1.0.0 is vulnerable", Severity.Error, project.Context.ProjectFile);

        var recorder = FindingSuppressions.Recording(root);
        recorder.IsSuppressed(issue);
        var baseline = new BaselineFile(BaselineFile.CurrentVersion, recorder.RecordedEntries);

        var changed = issue with { Description = "Lib 1.0.0 and Other 2.0.0 are vulnerable" };

        Assert.False(FindingSuppressions.Create(root, baseline).IsSuppressed(changed));
    }

    [Fact]
    public async Task ProjectAnalyzer_AppliesTheBaseline_ToFindingsWithoutALine()
    {
        using var project = TestProject.Create();
        File.WriteAllText(project.Context.ProjectFile, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var root = project.Context.RootDirectory;
        var rule = new SEC001AuthenticationNotConfigured();
        File.WriteAllText(Path.Combine(root, "Program.cs"), "var builder = WebApplication.CreateBuilder(args);");

        var recorder = FindingSuppressions.Recording(root);
        var recorded = await new ProjectAnalyzer([rule], recorder).AnalyzeAsync(root);
        var baseline = new BaselineFile(BaselineFile.CurrentVersion, recorder.RecordedEntries);

        var suppressions = FindingSuppressions.Create(root, baseline);
        var result = await new ProjectAnalyzer([rule], suppressions).AnalyzeAsync(root);

        Assert.Single(recorded.Issues);
        Assert.Empty(result.Issues);
        Assert.Null(Assert.Single(suppressions.Accepted).File);
    }

    // PERF003 and REL005 used to report a fixed message with no location, so one baseline entry would
    // have hidden every future blocking call or EnsureCreated.
    [Theory]
    [InlineData("PERF003", "task.Wait();", "other.Wait();")]
    [InlineData("REL005", "db.EnsureCreated();", "other.EnsureCreated();")]
    public async Task PatternRules_ReportANewOccurrence_DespiteTheBaseline(string ruleId, string accepted, string added)
    {
        IAnalysisRule rule = ruleId == "PERF003" ? new PERF003BlockingCallOnAsyncCode() : new REL005EnsureCreatedUsed();
        using var project = TestProject.Create(accepted);
        var baseline = await RecordBaseline(rule, project);

        Rewrite(project, $"{accepted}\n{added}");

        var issue = await Evaluate(rule, project, FindingSuppressions.Create(project.Context.RootDirectory, baseline));

        Assert.Equal(2, issue!.LineNumber);
    }

    // An issue with a line skips ProjectAnalyzer's filter, so a rule that reports a line without asking
    // about its occurrences could be neither suppressed nor baselined - and nothing else would notice.
    [Fact]
    public void EveryRuleThatReportsALine_AsksAboutItsOccurrences()
    {
        var rules = Directory.EnumerateFiles(FindRulesDirectory(), "*.cs", SearchOption.AllDirectories)
            .Select(file => (Name: Path.GetFileNameWithoutExtension(file), Code: File.ReadAllText(file)))
            .Where(rule => rule.Code.Contains("new AnalysisIssue(", StringComparison.Ordinal))
            .ToList();

        var locating = rules.Where(rule => Regex.IsMatch(rule.Code, @"\.Line\)+;")).ToList();
        var missing = locating
            .Where(rule => !rule.Code.Contains("Suppressions.IsSuppressed(Id,", StringComparison.Ordinal))
            .Select(rule => rule.Name);

        Assert.True(locating.Count >= 23, $"Expected at least 23 locating rules, found {locating.Count}.");
        Assert.Empty(missing);
    }

    private static Task<AnalysisIssue?> Evaluate(IAnalysisRule rule, TestProject project, FindingSuppressions? suppressions = null) =>
        rule.EvaluateAsync(project.Context with
        {
            Suppressions = suppressions ?? FindingSuppressions.Create(project.Context.RootDirectory, baseline: null),
        });

    private static async Task<BaselineFile> RecordBaseline(IAnalysisRule rule, TestProject project)
    {
        var recorder = FindingSuppressions.Recording(project.Context.RootDirectory);
        await rule.EvaluateAsync(project.Context with { Suppressions = recorder });

        return new BaselineFile(BaselineFile.CurrentVersion, recorder.RecordedEntries);
    }

    private static void Rewrite(TestProject project, string code) =>
        File.WriteAllText(Path.Combine(project.Context.RootDirectory, "Program.cs"), code);

    private static string FindRulesDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var rules = Path.Combine(directory.FullName, "Feature", "AspireForge.Analyzers", "Rules");

            if (Directory.Exists(rules))
            {
                return rules;
            }
        }

        throw new DirectoryNotFoundException("Could not find the rules directory above the test output.");
    }
}
