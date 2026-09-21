using AspireForge.Analyzers.Rules.Observability;

namespace AspireForge.Analyzers.Tests.Rules.Observability;

public class OBS001Tests
{
    private readonly OBS001OpenTelemetryMissing _rule = new();

    [Fact]
    public async Task Fires_WhenNoOpenTelemetryRegistrationIsFound()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("OBS001", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenAddOpenTelemetryHasASignalWiredToIt()
    {
        using var project = TestProject.Create(
            "builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(\"App\"));");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task Fires_WhenAddOpenTelemetryHasNoSignalWiredToIt()
    {
        using var project = TestProject.Create("builder.Services.AddOpenTelemetry();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Contains("WithTracing", issue!.Description);
    }
}
