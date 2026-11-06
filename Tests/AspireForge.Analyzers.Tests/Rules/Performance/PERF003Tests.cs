using AspireForge.Analyzers.Rules.Performance;

namespace AspireForge.Analyzers.Tests.Rules.Performance;

public class PERF003Tests
{
    private readonly PERF003BlockingCallOnAsyncCode _rule = new();

    [Fact]
    public async Task Fires_WhenGetAwaiterGetResultIsUsed()
    {
        using var project = TestProject.Create("var result = SomeAsyncMethod().GetAwaiter().GetResult();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("PERF003", issue!.RuleId);
    }

    [Fact]
    public async Task Fires_WhenWaitIsUsed()
    {
        using var project = TestProject.Create("SomeAsyncMethod().Wait();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Fact]
    public async Task ReportsWhereEachCallIs()
    {
        using var project = TestProject.Create("var ok = 1;\nFirst().Wait();\nvar x = Second().GetAwaiter().GetResult();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(2, issue!.LineNumber);
        Assert.EndsWith("Program.cs", issue.FilePath);
        Assert.Contains("(Program.cs:2, Program.cs:3)", issue.Description);
    }

    [Fact]
    public async Task DoesNotFire_WhenCodeIsProperlyAwaited()
    {
        using var project = TestProject.Create("var result = await SomeAsyncMethod();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenBlockingCallIsOnlyMentionedInAComment()
    {
        using var project = TestProject.Create("// avoid: SomeAsyncMethod().GetAwaiter().GetResult();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
