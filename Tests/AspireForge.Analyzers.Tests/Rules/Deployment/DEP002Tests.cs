using AspireForge.Analyzers.Rules.Deployment;
using AspireForge.Analyzers.Vulnerabilities;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Deployment;

public class DEP002Tests
{
    // Real output of `dotnet list package --vulnerable --include-transitive --format json` (SDK 10.0.301)
    // for a project referencing Microsoft.OpenApi 2.0.0.
    private const string VulnerableReport = """
        {
          "version": 1,
          "parameters": "--vulnerable --include-transitive",
          "sources": [ "https://api.nuget.org/v3/index.json" ],
          "projects": [
            {
              "path": "C:/src/Probe/Probe.csproj",
              "frameworks": [
                {
                  "framework": "net10.0",
                  "topLevelPackages": [
                    {
                      "id": "Microsoft.OpenApi",
                      "requestedVersion": "2.0.0",
                      "resolvedVersion": "2.0.0",
                      "vulnerabilities": [
                        { "severity": "High", "advisoryurl": "https://github.com/advisories/GHSA-v5pm-xwqc-g5wc" }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    // Real output for a project with nothing vulnerable: no "frameworks" key at all.
    private const string CleanReport = """
        { "version": 1, "parameters": "--vulnerable --include-transitive", "projects": [ { "path": "C:/src/App.Api.csproj" } ] }
        """;

    // Real output when the project can't be restored.
    private const string FailedReport = """
        { "version": 1, "problems": [ { "text": "Restore failed. Run `dotnet restore` for more details on the issue.", "level": "error" } ] }
        """;

    [Fact]
    public void Parser_ReadsTopLevelVulnerabilities()
    {
        var result = VulnerabilityReportParser.Parse(VulnerableReport);

        Assert.Null(result.Problem);
        var vulnerability = Assert.Single(result.Vulnerabilities);
        Assert.Equal("Microsoft.OpenApi", vulnerability.PackageId);
        Assert.Equal("2.0.0", vulnerability.ResolvedVersion);
        Assert.Equal("High", vulnerability.Severity);
        Assert.False(vulnerability.IsTransitive);
        Assert.Equal("https://github.com/advisories/GHSA-v5pm-xwqc-g5wc", vulnerability.AdvisoryUrl);
    }

    [Fact]
    public void Parser_ReturnsNothing_ForACleanProject()
    {
        var result = VulnerabilityReportParser.Parse(CleanReport);

        Assert.Null(result.Problem);
        Assert.Empty(result.Vulnerabilities);
    }

    [Fact]
    public void Parser_ReportsProblems_WhenTheScanFailed()
    {
        var result = VulnerabilityReportParser.Parse(FailedReport);

        Assert.Equal("Restore failed. Run `dotnet restore` for more details on the issue.", result.Problem);
    }

    [Fact]
    public void Parser_ReadsTransitivePackages_AndDeduplicatesAcrossFrameworks()
    {
        const string json = """
            {
              "projects": [ { "frameworks": [
                { "framework": "net8.0", "transitivePackages": [
                  { "id": "System.Text.Json", "resolvedVersion": "8.0.4", "vulnerabilities": [ { "severity": "Moderate", "advisoryurl": "https://x/1" } ] } ] },
                { "framework": "net10.0", "transitivePackages": [
                  { "id": "System.Text.Json", "resolvedVersion": "8.0.4", "vulnerabilities": [ { "severity": "Moderate", "advisoryurl": "https://x/1" } ] } ] }
              ] } ]
            }
            """;

        var vulnerability = Assert.Single(VulnerabilityReportParser.Parse(json).Vulnerabilities);
        Assert.True(vulnerability.IsTransitive);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    public void Parser_ReportsAProblem_ForUnreadableOutput(string output)
    {
        Assert.NotNull(VulnerabilityReportParser.Parse(output).Problem);
    }

    [Fact]
    public async Task Fires_AsAnError_ForAHighSeverityAdvisory()
    {
        var issue = await Evaluate(VulnerabilityReportParser.Parse(VulnerableReport));

        Assert.NotNull(issue);
        Assert.Equal("DEP002", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal("Vulnerable packages", issue.Title);
        Assert.Contains("Microsoft.OpenApi 2.0.0 (high, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)", issue.Description);
        Assert.Equal("Api.csproj", Path.GetFileName(issue.FilePath));
    }

    [Theory]
    [InlineData("Critical", Severity.Error)]
    [InlineData("High", Severity.Error)]
    [InlineData("Moderate", Severity.Warning)]
    [InlineData("Low", Severity.Suggestion)]
    public async Task MapsAdvisorySeverity(string advisory, Severity expected)
    {
        var issue = await Evaluate(Result(new PackageVulnerability("Pkg", "1.0.0", false, advisory, "https://x")));

        Assert.Equal(expected, issue!.Severity);
    }

    [Fact]
    public async Task UsesTheMostSevereAdvisory_AndMentionsTransitiveFixes()
    {
        var issue = await Evaluate(Result(
            new PackageVulnerability("A.Low", "1.0.0", false, "Low", "https://x/low"),
            new PackageVulnerability("Z.Critical", "2.0.0", true, "Critical", "https://x/critical")));

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.StartsWith("Packages with known security vulnerabilities: Z.Critical 2.0.0 (critical, transitive", issue.Description);
        Assert.Contains("for a transitive package", issue.Description);
    }

    [Fact]
    public async Task DoesNotFire_WhenNothingIsVulnerable()
    {
        var issue = await Evaluate(VulnerabilityReportParser.Parse(CleanReport));

        Assert.Null(issue);
    }

    [Fact]
    public async Task ReportsAnInfoFinding_WhenTheScanCouldNotRun()
    {
        var issue = await Evaluate(VulnerabilityReportParser.Parse(FailedReport));

        Assert.Equal(Severity.Info, issue!.Severity);
        Assert.Equal("Vulnerability scan couldn't run", issue.Title);
        Assert.Contains("Restore failed", issue.Description);
        Assert.DoesNotContain("..", issue.Description);
    }

    [Fact]
    public void IsOnlyInTheRuleSet_WhenRequested()
    {
        Assert.DoesNotContain(AnalysisRuleSet.CreateDefault(), rule => rule.Id == "DEP002");
        Assert.Contains(AnalysisRuleSet.CreateDefault(includeVulnerabilityScan: true), rule => rule.Id == "DEP002");
    }

    private static VulnerabilityScanResult Result(params PackageVulnerability[] vulnerabilities) => new(vulnerabilities, null);

    private static Task<AnalysisIssue?> Evaluate(VulnerabilityScanResult result) =>
        new DEP002VulnerablePackages(new FakeScanner(result)).EvaluateAsync(new ProjectContext
        {
            RootDirectory = Path.GetTempPath(),
            ProjectFile = Path.Combine(Path.GetTempPath(), "Api.csproj"),
            ProjectName = "Api",
            SourceFiles = [],
            ProjectReferences = [],
            PackageReferences = [],
        });

    private sealed class FakeScanner(VulnerabilityScanResult result) : IVulnerablePackageScanner
    {
        public Task<VulnerabilityScanResult> ScanAsync(string projectFile, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
