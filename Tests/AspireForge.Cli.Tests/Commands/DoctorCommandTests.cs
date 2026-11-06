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

    private const string Cors = "var builder = WebApplication.CreateBuilder(args);\npolicy.AllowAnyOrigin();\n";

    private string ProgramFile => Path.Combine(_projectDirectory, "Program.cs");

    private string BaselineFilePath => Path.Combine(_projectDirectory, ".aspireforge", "baseline.json");

    private async Task<(int ExitCode, string Output)> Doctor(
        OutputFormat format = OutputFormat.Text,
        bool updateBaseline = false,
        bool ignoreBaseline = false,
        bool summaryOnly = false)
    {
        var writer = new StringWriter();
        var exitCode = await new DoctorCommand(new ConsoleRenderer(writer)).RunAsync(
            _projectDirectory,
            format: format,
            updateBaseline: updateBaseline,
            ignoreBaseline: ignoreBaseline,
            summaryOnly: summaryOnly);

        return (exitCode, writer.ToString());
    }

    [Fact]
    public async Task InlineSuppression_ClearsTheFailure_AndTheReportSaysWhy()
    {
        File.WriteAllText(ProgramFile, Cors);
        Assert.Equal(ExitCodes.AnalysisFoundErrors, (await Doctor()).ExitCode);

        File.WriteAllText(
            ProgramFile,
            "var builder = WebApplication.CreateBuilder(args);\n"
                + "// aspireforge-disable-next-line SEC002: public read-only API\npolicy.AllowAnyOrigin();\n");

        var (exitCode, output) = await Doctor();

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Accepted (1)", output);
        Assert.Contains("SEC002", output);
        Assert.Contains("suppressed: public read-only API", output);
        Assert.Contains("Not counted in the score: 1 suppressed inline, 0 baselined.", output);
    }

    // An accepted problem is still a problem; a tick would say it isn't there.
    [Fact]
    public async Task AcceptedRule_IsNotShownAsPassed()
    {
        File.WriteAllText(ProgramFile, Cors);
        await Doctor(updateBaseline: true);

        var (_, output) = await Doctor();

        Assert.DoesNotContain("CORS policy restricts origins", output);
        Assert.Matches(@"- CORS: accepted\s+SEC002", output);
    }

    [Fact]
    public async Task UpdateBaseline_WritesTheFile_AndReportsWhatItAccepted()
    {
        File.WriteAllText(ProgramFile, Cors);

        var (exitCode, output) = await Doctor(updateBaseline: true);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(BaselineFilePath));
        Assert.Contains("Baseline written to", output);
        Assert.Matches(@"SEC002\s+1", output);
    }

    [Fact]
    public async Task Baseline_LetsCiPass_UntilANewFindingAppears()
    {
        File.WriteAllText(ProgramFile, Cors);
        await Doctor(updateBaseline: true);

        Assert.Equal(ExitCodes.Success, (await Doctor()).ExitCode);

        File.WriteAllText(ProgramFile, Cors + "other.AllowAnyOrigin();\n");

        var (exitCode, output) = await Doctor();

        Assert.Equal(ExitCodes.AnalysisFoundErrors, exitCode);
        Assert.Contains("Program.cs:3", output);
    }

    [Fact]
    public async Task NoBaseline_ReportsEverythingTheBaselineAccepts()
    {
        File.WriteAllText(ProgramFile, Cors);
        await Doctor(updateBaseline: true);

        var (exitCode, output) = await Doctor(ignoreBaseline: true);

        Assert.Equal(ExitCodes.AnalysisFoundErrors, exitCode);
        Assert.DoesNotContain("Accepted (", output);
        Assert.Contains("SEC002", output);
    }

    // The score leaves baselined findings out, so it must say so next to the number.
    [Fact]
    public async Task Baseline_IsDisclosedNextToTheScore()
    {
        File.WriteAllText(ProgramFile, Cors);
        await Doctor(updateBaseline: true);

        var (_, output) = await Doctor(summaryOnly: true);

        Assert.Contains("Production Readiness: 10/10 (100/100)", output);
        Assert.Matches(@"Not counted in the score: 0 suppressed inline, \d+ baselined\.", output);
        Assert.Contains("--no-baseline", output);
        Assert.DoesNotContain("Accepted (", output);
    }

    [Fact]
    public async Task Baseline_SaysWhenAnEntryNoLongerMatches()
    {
        File.WriteAllText(ProgramFile, Cors);
        await Doctor(updateBaseline: true);

        File.WriteAllText(ProgramFile, "var builder = WebApplication.CreateBuilder(args);\n");

        var (_, output) = await Doctor();

        Assert.Contains("1 baseline entry no longer matches anything.", output);
    }

    // Without --vulnerable DEP002 doesn't run, so its entries can't be judged - or dropped.
    [Fact]
    public async Task UpdateBaseline_KeepsEntriesForRulesThatDidNotRun()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BaselineFilePath)!);
        File.WriteAllText(
            BaselineFilePath,
            """{ "version": 1, "findings": [{ "ruleId": "DEP002", "file": "Sample.csproj", "fingerprint": "abc", "count": 1 }] }""");

        await Doctor(updateBaseline: true);

        Assert.Contains("DEP002", File.ReadAllText(BaselineFilePath));
    }

    [Fact]
    public async Task UpdateBaseline_ReplacesEntriesForRulesThatRan()
    {
        File.WriteAllText(ProgramFile, Cors);
        await Doctor(updateBaseline: true);
        var original = File.ReadAllText(BaselineFilePath);

        File.WriteAllText(ProgramFile, "var builder = WebApplication.CreateBuilder(args);\n");
        await Doctor(updateBaseline: true);

        Assert.Contains("SEC002", original);
        Assert.DoesNotContain("SEC002", File.ReadAllText(BaselineFilePath));
    }

    [Fact]
    public async Task DamagedBaseline_StopsWithAClearMessage()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BaselineFilePath)!);
        File.WriteAllText(BaselineFilePath, "not json");

        var (exitCode, output) = await Doctor();

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("isn't valid JSON", output);
    }

    [Fact]
    public async Task UpdateBaseline_RepairsADamagedBaseline()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BaselineFilePath)!);
        File.WriteAllText(BaselineFilePath, "not json");

        Assert.Equal(ExitCodes.Success, (await Doctor(updateBaseline: true)).ExitCode);
        Assert.Equal(ExitCodes.Success, (await Doctor()).ExitCode);
    }

    [Fact]
    public async Task JsonReport_CountsWhatWasAccepted()
    {
        File.WriteAllText(ProgramFile, Cors);
        await Doctor(updateBaseline: true);
        File.AppendAllText(ProgramFile, "other.AllowAnyOrigin(); // aspireforge-disable-line SEC002\n");

        var (_, output) = await Doctor(format: OutputFormat.Json);

        using var document = JsonDocument.Parse(output);
        Assert.Equal(1, document.RootElement.GetProperty("suppressed").GetInt32());
        Assert.True(document.RootElement.GetProperty("baselined").GetInt32() >= 1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectDirectory))
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
    }
}
