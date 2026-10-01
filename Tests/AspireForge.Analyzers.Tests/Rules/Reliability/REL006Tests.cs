using AspireForge.Analyzers.Rules.Reliability;

namespace AspireForge.Analyzers.Tests.Rules.Reliability;

public class REL006Tests
{
    private readonly REL006DatabaseRetryPolicyMissing _rule = new();

    [Theory]
    [InlineData("options.UseNpgsql(connectionString);")]
    [InlineData("options.UseSqlServer(connectionString);")]
    public async Task Fires_WhenProviderIsUsedWithoutRetry(string code)
    {
        using var project = TestProject.Create(code);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("REL006", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenRetryOnFailureIsEnabled()
    {
        using var project = TestProject.Create("options.UseNpgsql(cs, npgsql => npgsql.EnableRetryOnFailure());");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenNoRelationalProviderIsUsed()
    {
        using var project = TestProject.Create("var x = 1;");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }
}
