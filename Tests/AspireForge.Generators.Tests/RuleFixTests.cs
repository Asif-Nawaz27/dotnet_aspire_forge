using AspireForge.Core.Features;
using AspireForge.Core.Fixes;
using AspireForge.Core.Generation;
using AspireForge.Generators.Fixes;
using AspireForge.Generators.Project;

namespace AspireForge.Generators.Tests;

public class RuleFixTests : IDisposable
{
    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task REL001Fix_AddsHealthChecks_ToAFreshlyGeneratedProject()
    {
        var context = Generate("SampleApi");
        var programCsPath = ProgramCsPath(context);

        await Apply(context, new REL001HealthChecksFix());

        var content = File.ReadAllText(programCsPath);
        Assert.Contains("builder.Services.AddHealthChecks();", content);
        Assert.Contains("app.MapHealthChecks(\"/health\");", content);
    }

    [Fact]
    public async Task SEC003Fix_AddsHttpsRedirection_WhenMissing()
    {
        var context = Generate("SampleApi");
        var programCsPath = ProgramCsPath(context);

        // The webapi template already includes this by default - remove it to exercise the fix
        // against the scenario SEC003 actually fires on.
        File.WriteAllLines(programCsPath, File.ReadAllLines(programCsPath).Where(line => !line.Contains("UseHttpsRedirection")));
        Assert.DoesNotContain("UseHttpsRedirection", File.ReadAllText(programCsPath));

        await Apply(context, new SEC003HttpsRedirectionFix());

        Assert.Contains("app.UseHttpsRedirection();", File.ReadAllText(programCsPath));
    }

    [Fact]
    public async Task Fixes_ResultInAProjectThatStillBuilds()
    {
        var context = Generate("SampleApi");

        await Apply(context, new REL001HealthChecksFix(), new SEC003HttpsRedirectionFix());

        DotnetCli.Run(["build"], context.RootPath, TimeSpan.FromMinutes(3));
    }

    private static async Task Apply(FeatureContext context, params IRuleFix[] fixes)
    {
        var workspace = new FixWorkspace(context);

        foreach (var fix in fixes)
        {
            await fix.ApplyAsync(workspace);
        }

        workspace.Commit();
    }

    private FeatureContext Generate(string projectName)
    {
        var generator = new CleanArchitectureGenerator();
        var result = generator.Generate(new GenerationOptions
        {
            ProjectName = projectName,
            Architecture = "clean",
            OutputPath = _outputPath,
        });

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));

        return new FeatureContext { ProjectName = projectName, RootPath = Path.Combine(_outputPath, projectName) };
    }

    private static string ProgramCsPath(FeatureContext context) =>
        Path.Combine(context.RootPath, "src", $"{context.ProjectName}.Api", "Program.cs");

    public void Dispose()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }
    }
}
