using AspireForge.Analyzers.Rules.Testing;

namespace AspireForge.Analyzers.Tests.Rules.Testing;

public class TEST002Tests
{
    private readonly TEST002NoIntegrationTestsDetected _rule = new();

    [Fact]
    public async Task Fires_WhenNoIntegrationTestProjectExists()
    {
        using var solution = SolutionLayout.Create("MyApi", testProjectNames: ["MyApi.Tests"]);

        var issue = await _rule.EvaluateAsync(solution.ApiContext);

        Assert.NotNull(issue);
        Assert.Equal("TEST002", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenAnIntegrationTestProjectExists()
    {
        using var solution = SolutionLayout.Create("MyApi", testProjectNames: ["MyApi.IntegrationTests"]);

        var issue = await _rule.EvaluateAsync(solution.ApiContext);

        Assert.Null(issue);
    }
}
