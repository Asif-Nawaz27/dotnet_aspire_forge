using AspireForge.Analyzers.Rules.Performance;

namespace AspireForge.Analyzers.Tests.Rules.Performance;

public class PERF002Tests
{
    private readonly PERF002RateLimitingMissing _rule = new();

    [Fact]
    public async Task Fires_WhenWebProjectHasNoRateLimiting()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("PERF002", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenRateLimitingIsConfigured()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\nbuilder.Services.AddRateLimiter(options => { });");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenNotAWebProject()
    {
        using var project = TestProject.Create("public class Worker { }");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
