using AspireForge.Analyzers.Rules.Observability;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Observability;

public class OBS007Tests
{
    private readonly OBS007VerboseLoggingByDefault _rule = new();

    // The template default - and this repository's App.
    [Fact]
    public async Task DoesNotFire_ForInformationDefaults()
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", """
            {
              "Logging": {
                "LogLevel": {
                  "Default": "Information",
                  "Microsoft.AspNetCore": "Warning"
                }
              }
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("Debug")]
    [InlineData("Trace")]
    [InlineData("debug")]
    public async Task Fires_AsAWarning_ForAVerboseDefault_WithFileAndLine(string level)
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", $$"""
            {
              "Logging": {
                "LogLevel": {
                  "Default": "{{level}}"
                }
              }
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("OBS007", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Equal("Verbose logging by default", issue.Title);
        Assert.Equal(4, issue.LineNumber);
        Assert.Contains($"'Logging:LogLevel:Default' is {level} (appsettings.json:4)", issue.Description);
    }

    [Fact]
    public async Task Fires_ForProviderSpecificDefaults()
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", """{ "Logging": { "Console": { "LogLevel": { "Default": "Trace" } } } }""");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Contains("'Logging:Console:LogLevel:Default' is Trace", issue.Description);
    }

    [Fact]
    public async Task Suggests_ForASingleVerboseCategory()
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", """
            { "Logging": { "LogLevel": { "Default": "Information", "Microsoft.EntityFrameworkCore": "Debug" } } }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Suggestion, issue!.Severity);
        Assert.Equal("Verbose logging for a category", issue.Title);
        Assert.Contains("'Logging:LogLevel:Microsoft.EntityFrameworkCore' is Debug", issue.Description);
    }

    [Theory]
    [InlineData("""{ "Serilog": { "MinimumLevel": "Debug" } }""", "'Serilog:MinimumLevel' is Debug", Severity.Warning)]
    [InlineData("""{ "Serilog": { "MinimumLevel": { "Default": "Verbose" } } }""", "'Serilog:MinimumLevel:Default' is Verbose", Severity.Warning)]
    [InlineData("""{ "Serilog": { "MinimumLevel": { "Default": "Information", "Override": { "Microsoft": "Debug" } } } }""", "'Serilog:MinimumLevel:Override:Microsoft' is Debug", Severity.Suggestion)]
    public async Task Fires_ForVerboseSerilogLevels(string json, string expected, Severity severity)
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", json);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(severity, issue!.Severity);
        Assert.Contains(expected, issue.Description);
    }

    [Theory]
    [InlineData("""{ "Serilog": { "MinimumLevel": "Information" } }""")]
    [InlineData("""{ "Logging": { "LogLevel": { "Default": "Warning" } } }""")]
    [InlineData("""{ "Logging": { "LogLevel": { "Default": "None" } } }""")]
    [InlineData("""{ "Diagnostics": { "Mode": "Debug" } }""")]
    [InlineData("""{ "Logging": { "Debug": { "IncludeScopes": "true" } } }""")]
    public async Task DoesNotFire_ForQuietLevelsOrUnrelatedSettings(string json)
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", json);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_ForTheDevelopmentFile()
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", """{ "Logging": { "LogLevel": { "Default": "Information" } } }""");
        Write(project, "appsettings.Development.json", """{ "Logging": { "LogLevel": { "Default": "Debug" } } }""");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenProductionOverridesTheBaseFile()
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", """{ "Logging": { "LogLevel": { "Default": "Debug" } } }""");
        Write(project, "appsettings.Production.json", """{ "Logging": { "LogLevel": { "Default": "Information" } } }""");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task StillFires_WhenProductionOverridesADifferentKey()
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", """{ "Logging": { "LogLevel": { "Default": "Debug" } } }""");
        Write(project, "appsettings.Production.json", """{ "Logging": { "LogLevel": { "Microsoft": "Warning" } } }""");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.EndsWith("appsettings.json", issue!.FilePath);
    }

    [Fact]
    public async Task Fires_ForAVerboseLevelInTheProductionFileItself()
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", """{ "Logging": { "LogLevel": { "Default": "Information" } } }""");
        Write(project, "appsettings.Production.json", """{ "Logging": { "LogLevel": { "Default": "Trace" } } }""");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("appsettings.Production.json", issue!.Description);
    }

    [Fact]
    public async Task ReportsTheDefaultWarningFirst()
    {
        using var project = TestProject.Create();
        Write(project, "appsettings.json", """
            { "Logging": { "LogLevel": { "Microsoft": "Debug", "Default": "Trace" } } }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.StartsWith("Configuration for deployed environments (outside appsettings.Development.json) logs at Debug/Trace: 'Logging:LogLevel:Default' is Trace", issue.Description);
        Assert.Contains("'Logging:LogLevel:Microsoft' is Debug", issue.Description);
    }

    private static void Write(TestProject project, string fileName, string json) =>
        File.WriteAllText(Path.Combine(project.Context.RootDirectory, fileName), json);
}
