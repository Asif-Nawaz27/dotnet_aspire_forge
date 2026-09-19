using AspireForge.Analyzers.Rules.Security;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC002Tests
{
    private readonly SEC002CorsPolicyAllowsUnrestrictedOrigins _rule = new();

    [Fact]
    public async Task Fires_WhenAllowAnyOriginIsUsed()
    {
        using var project = TestProject.Create("policy.AllowAnyOrigin();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC002", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenAllowAnyOriginIsAbsent()
    {
        using var project = TestProject.Create("policy.WithOrigins(\"https://example.com\");");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
