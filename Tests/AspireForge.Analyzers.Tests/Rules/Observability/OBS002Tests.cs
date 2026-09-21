using AspireForge.Analyzers.Rules.Observability;

namespace AspireForge.Analyzers.Tests.Rules.Observability;

public class OBS002Tests
{
    private readonly OBS002StructuredLoggingMissing _rule = new();

    [Fact]
    public async Task Fires_WhenNoStructuredLoggingIsConfigured()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("OBS002", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenSerilogIsReferencedAndWiredUp()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\nbuilder.Host.UseSerilog();",
            packageReferences: ["Serilog.AspNetCore"]);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task Fires_WhenSerilogIsReferencedButNeverWiredUp()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);", packageReferences: ["Serilog.AspNetCore"]);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Contains("referenced", issue!.Description);
    }

    [Fact]
    public async Task DoesNotFire_WhenOpenTelemetryLoggingIsConfigured()
    {
        using var project = TestProject.Create("builder.Logging.AddOpenTelemetry(options => { });");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
