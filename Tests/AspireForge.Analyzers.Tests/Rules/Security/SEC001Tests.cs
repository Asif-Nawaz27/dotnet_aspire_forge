using AspireForge.Analyzers.Rules.Security;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC001Tests
{
    private readonly SEC001AuthenticationNotConfigured _rule = new();

    [Fact]
    public async Task Fires_WhenWebProjectHasNoAuthentication()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC001", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenAuthenticationIsConfiguredAndMiddlewareIsAdded()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\n"
                + "builder.Services.AddAuthentication();\n"
                + "var app = builder.Build();\n"
                + "app.UseAuthentication();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task Fires_WhenAuthenticationIsRegisteredButMiddlewareIsNotAdded()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\nbuilder.Services.AddAuthentication();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Contains("UseAuthentication", issue!.Description);
    }

    [Fact]
    public async Task Fires_WhenAuthenticationIsOnlyMentionedInAComment()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\n// TODO: builder.Services.AddAuthentication();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenNotAWebProject()
    {
        using var project = TestProject.Create("public class Worker { }");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
