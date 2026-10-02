using AspireForge.Analyzers.Rules.Observability;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Observability;

public class OBS005Tests
{
    private const string WebApp = "var builder = WebApplication.CreateBuilder(args);\n";

    private readonly OBS005ConsoleOutputInsteadOfLogger _rule = new();

    [Fact]
    public async Task Fires_ForConsoleWriteLine_WithFileAndLine()
    {
        using var project = TestProject.Create(WebApp + "var app = builder.Build();\nConsole.WriteLine(\"started\");");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("OBS005", issue!.RuleId);
        Assert.Equal(Severity.Suggestion, issue.Severity);
        Assert.EndsWith("Program.cs", issue.FilePath);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains("Program.cs:3", issue.Description);
    }

    [Theory]
    [InlineData("Console.Write(\"x\");")]
    [InlineData("Console.Error.WriteLine(\"x\");")]
    public async Task Fires_ForOtherConsoleOutputForms(string code)
    {
        using var project = TestProject.Create(WebApp + code);

        Assert.NotNull(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenLoggerIsUsed()
    {
        using var project = TestProject.Create(WebApp + "logger.LogInformation(\"started\");");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenConsoleIsOnlyInAComment()
    {
        using var project = TestProject.Create(WebApp + "// Console.WriteLine(\"debug\");");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_ForNonWebProjects()
    {
        using var project = TestProject.Create("Console.WriteLine(\"hello\");");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_ForConsoleReadLine()
    {
        using var project = TestProject.Create(WebApp + "var line = Console.ReadLine();");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }
}
