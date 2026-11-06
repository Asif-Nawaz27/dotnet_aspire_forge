using AspireForge.Analyzers.Rules.Reliability;

namespace AspireForge.Analyzers.Tests.Rules.Reliability;

public class REL005Tests
{
    private readonly REL005EnsureCreatedUsed _rule = new();

    [Theory]
    [InlineData("context.Database.EnsureCreated();")]
    [InlineData("await context.Database.EnsureCreatedAsync();")]
    public async Task Fires_WhenEnsureCreatedIsCalled(string code)
    {
        using var project = TestProject.Create(code);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("REL005", issue!.RuleId);
    }

    [Fact]
    public async Task ReportsWhereEachCallIs()
    {
        using var project = TestProject.Create("var ok = 1;\ndb.Database.EnsureCreated();\nawait db.Database.EnsureCreatedAsync();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(2, issue!.LineNumber);
        Assert.EndsWith("Program.cs", issue.FilePath);
        Assert.Contains("(Program.cs:2, Program.cs:3)", issue.Description);
    }

    [Fact]
    public async Task DoesNotFire_WhenMigrateIsUsed()
    {
        using var project = TestProject.Create("context.Database.Migrate();");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenEnsureCreatedIsOnlyInAComment()
    {
        using var project = TestProject.Create("// never call context.Database.EnsureCreated();");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }
}
