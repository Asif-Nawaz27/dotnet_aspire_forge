using AspireForge.Analyzers.Rules.Performance;

namespace AspireForge.Analyzers.Tests.Rules.Performance;

public class PERF001Tests
{
    private readonly PERF001ResponseCompressionMissing _rule = new();

    [Fact]
    public async Task Fires_WhenWebProjectHasNoResponseCompression()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("PERF001", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenResponseCompressionIsConfigured()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\nbuilder.Services.AddResponseCompression();");

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
