using AspireForge.Analyzers.Rules.Reliability;

namespace AspireForge.Analyzers.Tests.Rules.Reliability;

public class REL002Tests
{
    private readonly REL002GlobalExceptionHandlingMissing _rule = new();

    [Fact]
    public async Task Fires_WhenWebProjectHasNoExceptionHandler()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("REL002", issue!.RuleId);
    }

    [Theory]
    [InlineData("app.UseExceptionHandler();")]
    [InlineData("builder.Services.AddExceptionHandler<GlobalExceptionHandler>();")]
    public async Task DoesNotFire_WhenAnExceptionHandlerIsConfigured(string handlerLine)
    {
        using var project = TestProject.Create($"var builder = WebApplication.CreateBuilder(args);\n{handlerLine}");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
