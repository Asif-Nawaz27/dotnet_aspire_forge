using AspireForge.Analyzers.Rules.Observability;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Observability;

public class OBS006Tests
{
    private readonly OBS006InterpolatedLogMessage _rule = new();

    [Fact]
    public async Task Fires_ForAnInterpolatedLogMessage_WithFileAndLine()
    {
        using var project = TestProject.Create("""
            public void SignIn(string userId)
            {
                _logger.LogInformation($"User {userId} signed in");
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("OBS006", issue!.RuleId);
        Assert.Equal(Severity.Suggestion, issue.Severity);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains("LogInformation at Program.cs:3", issue.Description);
    }

    [Theory]
    [InlineData("logger.LogDebug($\"Cache miss for {key}\");")]
    [InlineData("logger.LogWarning(\"Retrying order \" + orderId);")]
    [InlineData("logger.LogCritical(string.Format(\"Disk {0} full\", disk));")]
    [InlineData("logger.LogError(ex, $\"Failed to process {orderId}\");")]
    [InlineData("logger.LogError(new EventId(42), ex, $\"Failed to process {orderId}\");")]
    [InlineData("logger.Log(LogLevel.Warning, $\"Slow query took {elapsed} ms\");")]
    [InlineData("using var scope = logger.BeginScope($\"Order {orderId}\");")]
    [InlineData("logger.LogInformation($@\"Saved to {path}\");")]
    [InlineData("logger.LogTrace($\"\"\"\n  Payload {payload}\n  \"\"\");")]
    public async Task Fires_ForMessagesBuiltFromValues(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("logger.LogInformation(\"User {UserId} signed in\", userId);")]
    [InlineData("logger.LogError(ex, \"Failed to process {OrderId}\", orderId);")]
    [InlineData("logger.LogInformation(\"Order {OrderId}, total {Total}\", $\"{id}\", total);")]
    [InlineData("logger.LogInformation($\"Service started\");")]
    [InlineData("logger.LogInformation($\"Use {{braces}} literally\");")]
    [InlineData("logger.LogInformation(\"Part one, \" + \"part two\");")]
    [InlineData("logger.LogInformation(message);")]
    [InlineData("LogUserSignedIn(logger, userId);")]
    [InlineData("var text = $\"User {userId}\";")]
    [InlineData("// logger.LogInformation($\"User {userId}\");")]
    public async Task DoesNotFire_ForTemplatesConstantsAndNonLoggingCode(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("using Serilog;\nLog.Information($\"User {userId} signed in\");", "Information")]
    [InlineData("using Serilog;\n_log.Error(ex, \"Failed for \" + id);", "Error")]
    [InlineData("using NLog;\n_logger.Info($\"User {userId} signed in\");", "Info")]
    [InlineData("using NLog;\n_logger.Warn(string.Format(\"Disk {0}\", d));", "Warn")]
    public async Task Fires_ForSerilogAndNLog_WhenTheFileImportsThem(string source, string method)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains($"{method} at Program.cs:2", issue!.Description);
    }

    [Theory]
    [InlineData("var result = Result.Error($\"Order {id} not found\");")]
    [InlineData("validation.Warning($\"Field {name} is long\");")]
    [InlineData("using Serilog;\nLog.Information(\"User {UserId} signed in\", userId);")]
    public async Task DoesNotFire_ForLookAlikeMethodsOrSerilogTemplates(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ListsAtMostThreeCalls_AndCountsTheRest()
    {
        using var project = TestProject.Create(string.Join(
            "\n",
            Enumerable.Range(1, 5).Select(i => $"logger.LogInformation($\"Step {{step{i}}}\");")));

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("(and 2 more)", issue!.Description);
    }
}
