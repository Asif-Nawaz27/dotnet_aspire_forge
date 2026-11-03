using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC006Tests
{
    private const string Web = "var builder = WebApplication.CreateBuilder(args);\nvar app = builder.Build();\n";

    private readonly SEC006HstsMissing _rule = new();

    [Fact]
    public async Task Fires_WhenUseHstsIsNeverCalled()
    {
        using var project = TestProject.Create(Web + "app.UseHttpsRedirection();\napp.MapControllers();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC006", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Contains("No call to UseHsts was found", issue.Description);
    }

    // The ASP.NET Core template shapes - and this repository's App.
    [Theory]
    [InlineData("if (app.Environment.IsDevelopment())\n{\n    app.MapOpenApi();\n}\nelse\n{\n    app.UseHsts();\n}")]
    [InlineData("if (!app.Environment.IsDevelopment())\n{\n    app.UseExceptionHandler(\"/Error\");\n    app.UseHsts();\n}")]
    [InlineData("if (!app.Environment.IsDevelopment())\n    app.UseHsts();")]
    [InlineData("app.UseHsts();")]
    public async Task DoesNotFire_WhenHstsRunsOutsideDevelopment(string pipeline)
    {
        using var project = TestProject.Create(Web + pipeline);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("if (app.Environment.IsDevelopment())\n{\n    app.UseHsts();\n}", 5)]
    [InlineData("if (app.Environment.IsDevelopment())\n    app.UseHsts();", 4)]
    [InlineData("#if DEBUG\napp.UseHsts();\n#endif", 4)]
    public async Task Fires_WhenHstsIsOnlyEnabledInDevelopment_WithTheLine(string pipeline, int line)
    {
        using var project = TestProject.Create(Web + pipeline);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal(line, issue!.LineNumber);
        Assert.Contains("only called inside a development-only block (Program.cs:", issue.Description);
    }

    [Fact]
    public async Task DoesNotFire_WhenAnyCallRunsOutsideDevelopment()
    {
        using var project = TestProject.Create(Web + """
            if (app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseHsts();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("// app.UseHsts();")]
    [InlineData("builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));")]
    public async Task Fires_WhenHstsIsOnlyCommentedOutOrConfigured(string pipeline)
    {
        using var project = TestProject.Create(Web + pipeline);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Fact]
    public async Task DoesNotFire_ForNonWebProjects()
    {
        using var project = TestProject.Create("public class Worker { }");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
