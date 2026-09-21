using AspireForge.Analyzers.Rules.Reliability;

namespace AspireForge.Analyzers.Tests.Rules.Reliability;

public class REL001Tests
{
    private readonly REL001HealthChecksMissing _rule = new();

    [Fact]
    public async Task Fires_WhenWebProjectHasNoHealthChecks()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("REL001", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenHealthChecksAreConfiguredAndMapped()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\n"
                + "builder.Services.AddHealthChecks();\n"
                + "var app = builder.Build();\n"
                + "app.MapHealthChecks(\"/health\");");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task Fires_WhenHealthChecksAreRegisteredButNotMapped()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\nbuilder.Services.AddHealthChecks();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Contains("MapHealthChecks", issue!.Description);
    }

    [Fact]
    public async Task DoesNotFire_WhenNotAWebProject()
    {
        using var project = TestProject.Create("public class Worker { }");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
