using System.Text.Json;
using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Tests.Commands;

public class DoctorCommandTests : IDisposable
{
    private readonly string _projectDirectory =
        Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    public DoctorCommandTests()
    {
        Directory.CreateDirectory(_projectDirectory);
        File.WriteAllText(Path.Combine(_projectDirectory, "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        File.WriteAllText(Path.Combine(_projectDirectory, "Program.cs"), "var builder = WebApplication.CreateBuilder(args);");
    }

    [Fact]
    public async Task RunAsync_PrintsProjectNameAndDetectedIssues_ForTextFormat()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory);

        var output = writer.ToString();
        Assert.Contains("Project: Sample", output);
        Assert.Contains("Authentication not configured", output);
    }

    [Fact]
    public async Task RunAsync_ShowsADetailBlockPerFinding_WithNextSteps()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory);

        var output = writer.ToString();
        Assert.Contains("Findings (", output);
        Assert.Contains("⚠ SEC001 · Authentication not configured · warning", output);
        Assert.Contains("  Scope  whole project", output);
        Assert.Contains("  Why    No call to AddAuthentication was found.", output);
        Assert.Contains("  Fix    aspireforge add authentication", output);
        Assert.Contains("  Docs   https://github.com/Asif-Nawaz27/dotnet_aspire_forge/blob/main/docs/rules/security.md#sec001---authentication-not-configured", output);
        Assert.Contains("Next steps", output);
        Assert.Contains("aspireforge add authentication   resolves SEC001", output);
        Assert.Contains("Suggestions:", output);
    }

    [Fact]
    public async Task RunAsync_ShowsTheWeightedScore_WithAPerCategoryBreakdown()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory);

        var output = writer.ToString();
        Assert.Matches(@"Production Readiness: \d+/10 \(\d+/100\)", output);
        Assert.Matches(@"  Security\s+\d+/100   -\d+ \(", output);
        Assert.Contains("Score: 100 minus 30 per critical, 15 per error, 5 per warning", output);
    }

    // The cap wording itself is covered by ReadinessScoreTests; here, the property that matters.
    [Fact]
    public async Task RunAsync_NeverScoresAbove60_WhenAnErrorIsFound()
    {
        File.WriteAllText(Path.Combine(_projectDirectory, "Cors.cs"), "policy.AllowAnyOrigin();");
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory);

        var points = int.Parse(System.Text.RegularExpressions.Regex.Match(writer.ToString(), @"\((\d+)/100\)").Groups[1].Value);
        Assert.InRange(points, 0, 60);
    }

    [Fact]
    public async Task RunAsync_ShowsTheRuleIdNextToEachFailingCheck()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory);

        var checkLine = writer.ToString().Split('\n').Single(line => line.Contains("⚠ Authentication not configured"));
        Assert.EndsWith("SEC001", checkLine.TrimEnd());
    }

    [Fact]
    public async Task RunAsync_HidesTheDetails_WithSummary()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory, summaryOnly: true);

        var output = writer.ToString();
        Assert.Contains("Authentication not configured", output);
        Assert.DoesNotContain("Findings (", output);
        Assert.DoesNotContain("  Why", output);
        Assert.Contains("Next steps", output);
        Assert.Contains("run without --summary to see where and how", output);
    }

    // DEP002 is opt-in: without --vulnerable it's neither run nor shown, so it can't affect the score.
    [Fact]
    public async Task RunAsync_DoesNotShowTheVulnerabilityCheck_UnlessRequested()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory);

        Assert.DoesNotContain("vulnerable", writer.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsSuccess_WhenNoIssueMeetsTheDefaultErrorThreshold()
    {
        var command = new DoctorCommand(new ConsoleRenderer(new StringWriter()));

        var exitCode = await command.RunAsync(_projectDirectory);

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public async Task RunAsync_ReturnsAnalysisFoundErrors_WhenFailOnIsLoweredToWarning()
    {
        var command = new DoctorCommand(new ConsoleRenderer(new StringWriter()));

        var exitCode = await command.RunAsync(_projectDirectory, failOn: Severity.Warning);

        Assert.Equal(ExitCodes.AnalysisFoundErrors, exitCode);
    }

    [Fact]
    public async Task RunAsync_EmitsValidJson_ContainingTheDetectedIssues_ForJsonFormat()
    {
        var writer = new StringWriter();
        var command = new DoctorCommand(new ConsoleRenderer(writer));

        await command.RunAsync(_projectDirectory, format: OutputFormat.Json);

        using var document = JsonDocument.Parse(writer.ToString());
        var issues = document.RootElement.GetProperty("issues");

        Assert.True(issues.GetArrayLength() > 0);
        Assert.Contains(
            issues.EnumerateArray(),
            issue => issue.GetProperty("ruleId").GetString() == "SEC001");
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectDirectory))
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
    }
}
