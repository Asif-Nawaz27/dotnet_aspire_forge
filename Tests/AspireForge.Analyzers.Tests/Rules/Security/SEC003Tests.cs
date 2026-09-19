using AspireForge.Analyzers.Rules.Security;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC003Tests
{
    private readonly SEC003HttpsRedirectionMissing _rule = new();

    [Fact]
    public async Task Fires_WhenWebProjectHasNoHttpsRedirection()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC003", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenHttpsRedirectionIsConfigured()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\napp.UseHttpsRedirection();");

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
