using AspireForge.Analyzers;
using AspireForge.Core.Analysis;
using AspireForge.Core.Features;
using AspireForge.Core.Generation;
using AspireForge.Generators.Authentication;
using AspireForge.Generators.Observability;
using AspireForge.Generators.Project;

namespace AspireForge.IntegrationTests;

// The generator and the rules must agree: whatever `aspireforge new` produces has to pass
// `aspireforge doctor`, or a new user's first experience is the tool criticising its own output. If a
// rule changes or a new rule lands, this fails until the generator (or the rule) is fixed.
[Collection(nameof(GeneratedProjectBuildCollection))]
public class GeneratedProjectDoctorTests : IDisposable
{
    private readonly string _outputPath =
        Path.Combine(Path.GetTempPath(), "aspireforge-e2e", Guid.NewGuid().ToString("N"));

    // Authentication is the one deliberate finding: which scheme to use (JWT, cookies, Entra ID) is
    // the developer's decision, and doctor points straight at `aspireforge add authentication`.
    [Fact]
    public async Task NewProject_PassesEveryRule_ExceptTheAuthenticationDecision()
    {
        var root = Generate("DoctorApi");

        var issues = await AnalyzeApi(root, "DoctorApi");

        Assert.True(
            issues.Select(issue => issue.RuleId).SequenceEqual(["SEC001"]),
            "Unexpected findings on a freshly generated project:\n"
                + string.Join("\n", issues.Select(issue => $"  {issue.RuleId}: {issue.Description}")));
    }

    [Fact]
    public async Task NewProject_WithAuthenticationAdded_HasNoFindings()
    {
        var root = Generate("SecureApi");
        var context = new FeatureContext { ProjectName = "SecureApi", RootPath = root };
        await new AuthenticationFeatureInstaller().InstallAsync(context);

        var issues = await AnalyzeApi(root, "SecureApi");

        Assert.True(
            issues.Count == 0,
            "Unexpected findings after `add authentication`:\n"
                + string.Join("\n", issues.Select(issue => $"  {issue.RuleId}: {issue.Description}")));
    }

    // `new` already includes ServiceDefaults; running `add telemetry` afterwards must change nothing.
    [Fact]
    public async Task AddTelemetry_OnANewProject_IsANoOp()
    {
        var root = Generate("TelemetryTwiceApi");
        var programCs = Path.Combine(root, "src", "TelemetryTwiceApi.Api", "Program.cs");
        var before = await File.ReadAllTextAsync(programCs);

        var steps = await new TelemetryFeatureInstaller()
            .InstallAsync(new FeatureContext { ProjectName = "TelemetryTwiceApi", RootPath = root });

        Assert.True(FeatureSteps.IsAlreadyInstalled(steps));
        Assert.Equal(before, await File.ReadAllTextAsync(programCs));
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static async Task<IReadOnlyCollection<AnalysisIssue>> AnalyzeApi(string root, string projectName)
    {
        var analyzer = new ProjectAnalyzer(AnalysisRuleSet.CreateDefault());
        var result = await analyzer.AnalyzeAsync(Path.Combine(root, "src", $"{projectName}.Api"));
        return result.Issues;
    }

    private string Generate(string projectName)
    {
        var result = new CleanArchitectureGenerator().Generate(new GenerationOptions
        {
            ProjectName = projectName,
            Architecture = "clean",
            OutputPath = _outputPath,
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

        return Path.Combine(_outputPath, projectName);
    }
}
