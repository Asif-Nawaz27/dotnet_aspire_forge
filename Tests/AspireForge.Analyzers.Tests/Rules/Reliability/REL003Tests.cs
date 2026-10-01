using AspireForge.Analyzers.Rules.Reliability;

namespace AspireForge.Analyzers.Tests.Rules.Reliability;

public class REL003Tests
{
    private readonly REL003OutboundHttpWithoutResilience _rule = new();

    [Fact]
    public async Task Fires_WhenNewHttpClientIsUsed()
    {
        using var project = TestProject.Create("var client = new HttpClient();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("REL003", issue!.RuleId);
    }

    [Fact]
    public async Task Fires_WhenAddHttpClientHasNoResilienceHandler()
    {
        using var project = TestProject.Create("builder.Services.AddHttpClient<IFoo, Foo>();");

        Assert.NotNull(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenResilienceHandlerIsAdded()
    {
        using var project = TestProject.Create("builder.Services.AddHttpClient<IFoo, Foo>().AddStandardResilienceHandler();");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenResilienceIsAppliedViaClientDefaults()
    {
        using var project = TestProject.Create(
            "builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());\n"
                + "builder.Services.AddHttpClient<IFoo, Foo>();");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenNoHttpClientIsUsed()
    {
        using var project = TestProject.Create("var x = 1;");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenNewHttpClientIsOnlyInAComment()
    {
        using var project = TestProject.Create("// avoid: new HttpClient()");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }
}
