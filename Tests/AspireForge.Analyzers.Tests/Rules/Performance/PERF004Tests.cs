using AspireForge.Analyzers.Rules.Performance;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Performance;

public class PERF004Tests
{
    private readonly PERF004SynchronousIoAllowed _rule = new();

    [Fact]
    public async Task Fires_AsAWarning_ForServerWideKestrelSetting_WithFileAndLine()
    {
        using var project = TestProject.Create("""
            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AllowSynchronousIO = true;
            });
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("PERF004", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Equal(4, issue.LineNumber);
        Assert.Contains("server-wide at Program.cs:4", issue.Description);
    }

    [Theory]
    [InlineData("builder.Services.Configure<KestrelServerOptions>(o => o.AllowSynchronousIO = true);")]
    [InlineData("builder.Services.Configure<IISServerOptions>(o => { o.AllowSynchronousIO = true; });")]
    [InlineData("var server = new TestServer(builder) { AllowSynchronousIO = true };")]
    public async Task Fires_AsAWarning_ForOtherServerOptions(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
    }

    [Theory]
    [InlineData("var feature = HttpContext.Features.Get<IHttpBodyControlFeature>();\nif (feature != null) { feature.AllowSynchronousIO = true; }")]
    [InlineData("IHttpBodyControlFeature? bodyControl = GetFeature();\nbodyControl!.AllowSynchronousIO = true;")]
    [InlineData("context.Features.Get<IHttpBodyControlFeature>()!.AllowSynchronousIO = true;")]
    [InlineData("((IHttpBodyControlFeature)feature).AllowSynchronousIO = true;")]
    public async Task Suggests_ForThePerRequestFeature(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Suggestion, issue!.Severity);
        Assert.Contains("for this request", issue.Description);
    }

    [Fact]
    public async Task TreatsAnUnrelatedVariable_AsServerWide()
    {
        using var project = TestProject.Create(
            "builder.WebHost.ConfigureKestrel(kestrel =>\n{\n    kestrel.AllowSynchronousIO = true;\n});");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
    }

    [Fact]
    public async Task UsesSuggestionSeverity_WhenTheFeatureIsReachedThroughFeatures()
    {
        using var project = TestProject.Create("context.Features.Get<IHttpBodyControlFeature>()!.AllowSynchronousIO = true;");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Suggestion, issue!.Severity);
        Assert.Contains("for this request at Program.cs:1", issue.Description);
    }

    [Theory]
    [InlineData("options.AllowSynchronousIO = false;")]
    [InlineData("if (options.AllowSynchronousIO == true) { }")]
    [InlineData("// options.AllowSynchronousIO = true;")]
    [InlineData("var allow = options.AllowSynchronousIO;")]
    public async Task DoesNotFire_WhenSynchronousIoStaysDisabled(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenLimitedToDevelopment()
    {
        using var project = TestProject.Create("""
            builder.WebHost.ConfigureKestrel(options =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    options.AllowSynchronousIO = true;
                }
            });
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ReportsTheServerWideWarningFirst()
    {
        using var project = TestProject.Create("""
            context.Features.Get<IHttpBodyControlFeature>()!.AllowSynchronousIO = true;
            builder.WebHost.ConfigureKestrel(o => o.AllowSynchronousIO = true);
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Equal(2, issue.LineNumber);
        Assert.Contains("for this request at Program.cs:1", issue.Description);
    }
}
