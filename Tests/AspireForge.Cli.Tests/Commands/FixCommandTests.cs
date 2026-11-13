using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;

namespace AspireForge.Cli.Tests.Commands;

public class FixCommandTests : IDisposable
{
    // Reports both fixable rules: no health checks and no HTTPS redirection.
    private const string Unfixed = "var builder = WebApplication.CreateBuilder(args);\n\nvar app = builder.Build();\n\napp.MapGet(\"/\", () => \"Hello\");\n\napp.Run();\n";

    private const string Fixed = "var builder = WebApplication.CreateBuilder(args);\nbuilder.Services.AddHealthChecks();\nvar app = builder.Build();\napp.UseHttpsRedirection();\napp.MapHealthChecks(\"/health\");\napp.Run();\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    private string ApiDirectory => Path.Combine(_root, "src", "Sample.Api");

    private string ProgramFile => Path.Combine(ApiDirectory, "Program.cs");

    public FixCommandTests()
    {
        Directory.CreateDirectory(ApiDirectory);
        File.WriteAllText(Path.Combine(_root, "Sample.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(ApiDirectory, "Sample.Api.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        File.WriteAllText(ProgramFile, Unfixed);
    }

    private async Task<(int ExitCode, string Output)> Fix(string? ruleId = null, bool all = false, bool dryRun = false)
    {
        var writer = new StringWriter();
        var exitCode = await new FixCommand(new ConsoleRenderer(writer)).RunAsync(ruleId, all, dryRun, _root);

        return (exitCode, writer.ToString());
    }

    private async Task WriteBaselineForEverything()
    {
        await new DoctorCommand(new ConsoleRenderer(new StringWriter()))
            .RunAsync(ApiDirectory, updateBaseline: true);
    }

    [Fact]
    public async Task RunAsync_ReturnsInvalidConfiguration_ForARuleWithNoAvailableFix()
    {
        var (exitCode, output) = await Fix("ARCH001");

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("No automatic fix is available", output);
    }

    [Fact]
    public async Task RunAsync_ReturnsInvalidConfiguration_ForAnUnknownRuleId() =>
        Assert.Equal(ExitCodes.InvalidConfiguration, (await Fix("NOTAREALRULE")).ExitCode);

    [Fact]
    public async Task RunAsync_RefusesNeitherARuleNorAll()
    {
        var (exitCode, output) = await Fix();

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("Give a rule ID or --all.", output);
        Assert.Contains("REL001", output);
    }

    [Fact]
    public async Task RunAsync_RefusesBothARuleAndAll()
    {
        var (exitCode, output) = await Fix("REL001", all: true);

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("not both", output);
        Assert.Equal(Unfixed, File.ReadAllText(ProgramFile));
    }

    [Fact]
    public async Task RunAsync_RefusesAFolderWithoutASolution()
    {
        File.Delete(Path.Combine(_root, "Sample.slnx"));

        var (exitCode, output) = await Fix(all: true);

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("No solution file found", output);
    }

    [Fact]
    public async Task RunAsync_RefusesASolutionWithoutAnApiProject()
    {
        Directory.Delete(ApiDirectory, recursive: true);

        var (exitCode, output) = await Fix(all: true);

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("No Api project found", output);
    }

    [Fact]
    public async Task Rule_AppliesThatFixOnly()
    {
        var (exitCode, output) = await Fix("REL001");

        var program = File.ReadAllText(ProgramFile);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("REL001 fixed successfully.", output);
        Assert.Contains("builder.Services.AddHealthChecks();", program);
        Assert.DoesNotContain("UseHttpsRedirection", program);
    }

    [Fact]
    public async Task Rule_IsCaseInsensitive()
    {
        await Fix("rel001");

        Assert.Contains("AddHealthChecks", File.ReadAllText(ProgramFile));
    }

    // Regression test: a second run used to add the same lines again.
    [Fact]
    public async Task Rule_RunTwice_ChangesNothingTheSecondTime()
    {
        await Fix("REL001");
        var once = File.ReadAllText(ProgramFile);

        var (exitCode, output) = await Fix("REL001");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Nothing to fix: doctor doesn't report REL001.", output);
        Assert.Equal(once, File.ReadAllText(ProgramFile));
    }

    [Fact]
    public async Task All_AppliesEveryFixDoctorCallsFor()
    {
        var (exitCode, output) = await Fix(all: true);

        var program = File.ReadAllText(ProgramFile);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("REL001 fixed successfully.", output);
        Assert.Contains("SEC003 fixed successfully.", output);
        Assert.Contains("app.MapHealthChecks(\"/health\");", program);
        Assert.Contains("app.UseHttpsRedirection();", program);
    }

    [Fact]
    public async Task All_LeavesAProjectThatAlreadyPassesAlone()
    {
        File.WriteAllText(ProgramFile, Fixed);

        var (exitCode, output) = await Fix(all: true);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Nothing to fix", output);
        Assert.Equal(Fixed, File.ReadAllText(ProgramFile));
    }

    [Fact]
    public async Task All_OnlyAppliesFixesForRulesThatFail()
    {
        File.WriteAllText(ProgramFile, Unfixed.Replace("var app = builder.Build();\n", "var app = builder.Build();\napp.UseHttpsRedirection();\n"));

        var (_, output) = await Fix(all: true);

        Assert.Contains("REL001 fixed successfully.", output);
        Assert.DoesNotContain("SEC003", output);
        Assert.Single(File.ReadAllLines(ProgramFile), line => line.Contains("UseHttpsRedirection"));
    }

    [Fact]
    public async Task All_RunTwice_ChangesNothingTheSecondTime()
    {
        await Fix(all: true);
        var once = File.ReadAllText(ProgramFile);

        await Fix(all: true);

        Assert.Equal(once, File.ReadAllText(ProgramFile));
    }

    // What's been accepted stays accepted: a bulk fix mustn't overrule it.
    [Fact]
    public async Task All_LeavesBaselinedFindingsAlone()
    {
        await WriteBaselineForEverything();

        var (_, output) = await Fix(all: true);

        Assert.Contains("Nothing to fix", output);
        Assert.Equal(Unfixed, File.ReadAllText(ProgramFile));
    }

    [Fact]
    public async Task All_LeavesDisabledRulesAlone()
    {
        Directory.CreateDirectory(Path.Combine(ApiDirectory, ".aspireforge"));
        File.WriteAllText(
            Path.Combine(ApiDirectory, ".aspireforge", "config.json"),
            """{ "rules": { "SEC003": { "enabled": false } } }""");

        await Fix(all: true);

        var program = File.ReadAllText(ProgramFile);

        Assert.Contains("AddHealthChecks", program);
        Assert.DoesNotContain("UseHttpsRedirection", program);
    }

    [Fact]
    public async Task All_StopsOnADamagedBaseline()
    {
        Directory.CreateDirectory(Path.Combine(ApiDirectory, ".aspireforge"));
        File.WriteAllText(Path.Combine(ApiDirectory, ".aspireforge", "baseline.json"), "not json");

        var (exitCode, output) = await Fix(all: true);

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("isn't valid JSON", output);
        Assert.Equal(Unfixed, File.ReadAllText(ProgramFile));
    }

    // Naming a rule is a deliberate choice to fix it, baselined or not.
    [Fact]
    public async Task Rule_FixesABaselinedFinding()
    {
        await WriteBaselineForEverything();

        await Fix("REL001");

        Assert.Contains("AddHealthChecks", File.ReadAllText(ProgramFile));
    }

    [Fact]
    public async Task DryRun_ChangesNothing_AndShowsTheDiff()
    {
        var (exitCode, output) = await Fix(all: true, dryRun: true);

        // 1, so CI can fail on fixes left undone.
        Assert.Equal(ExitCodes.AnalysisFoundErrors, exitCode);
        Assert.Equal(Unfixed, File.ReadAllText(ProgramFile));
        Assert.Contains("Dry run: nothing was changed.", output);
        Assert.Contains("REL001", output);
        Assert.Contains("SEC003", output);
        Assert.Contains("--- src/Sample.Api/Program.cs", output);
        Assert.Contains("+builder.Services.AddHealthChecks();", output);
        Assert.Contains("+app.UseHttpsRedirection();", output);
        Assert.Contains("1 file would change.", output);
    }

    [Fact]
    public async Task DryRun_WorksForASingleRule()
    {
        var (_, output) = await Fix("SEC003", dryRun: true);

        Assert.Equal(Unfixed, File.ReadAllText(ProgramFile));
        Assert.Contains("+app.UseHttpsRedirection();", output);
        Assert.DoesNotContain("AddHealthChecks", output);
    }

    // The preview is only worth trusting if it's what really happens.
    [Fact]
    public async Task DryRun_ShowsExactlyWhatTheRealRunWrites()
    {
        var (_, preview) = await Fix(all: true, dryRun: true);

        await Fix(all: true);

        var added = preview.ReplaceLineEndings("\n").Split('\n')
            .Where(line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal))
            .Select(line => line[1..])
            .ToList();
        var written = File.ReadAllText(ProgramFile).Split('\n').ToList();

        foreach (var line in Unfixed.Split('\n'))
        {
            written.Remove(line);
        }

        Assert.Equal(added.Order(), written.Order());
    }

    [Fact]
    public async Task DryRun_HasNothingToShow_WhenTheProjectPasses()
    {
        File.WriteAllText(ProgramFile, Fixed);

        var (exitCode, output) = await Fix(all: true, dryRun: true);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Nothing to fix", output);
        Assert.DoesNotContain("---", output);
    }

    [Fact]
    public async Task AFailedFix_WritesNothing_AndSaysSo()
    {
        const string NoRunCall = "var builder = WebApplication.CreateBuilder(args);\nvar app = builder.Build();\n";
        File.WriteAllText(ProgramFile, NoRunCall);

        var (exitCode, output) = await Fix(all: true);

        Assert.Equal(ExitCodes.GenerationFailure, exitCode);
        Assert.Contains("Failed to fix 'REL001'", output);
        Assert.Contains("Nothing was changed.", output);
        Assert.Equal(NoRunCall, File.ReadAllText(ProgramFile));
    }

    // Rewriting every line ending would make a one-line fix look like a whole-file change.
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Fix_KeepsTheFilesLineEndings(string newLine)
    {
        File.WriteAllText(ProgramFile, Unfixed.Replace("\n", newLine));

        await Fix(all: true);

        var program = File.ReadAllText(ProgramFile);

        Assert.Equal(program.Replace("\r\n", "\n").Replace("\n", newLine), program);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
