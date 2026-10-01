using AspireForge.Analyzers.Rules.Reliability;

namespace AspireForge.Analyzers.Tests.Rules.Reliability;

public class REL007Tests
{
    private readonly REL007SettingsNotValidatedAtStartup _rule = new();

    [Fact]
    public async Task Fires_WhenConfigureBindsASection()
    {
        using var project = TestProject.Create("services.Configure<JwtOptions>(configuration.GetSection(\"Jwt\"));");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("REL007", issue!.RuleId);
    }

    [Fact]
    public async Task Fires_WhenAddOptionsBindsWithoutValidateOnStart()
    {
        using var project = TestProject.Create(
            "services.AddOptions<JwtOptions>().BindConfiguration(\"Jwt\").ValidateDataAnnotations();");

        Assert.NotNull(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenAddOptionsBindsWithValidateOnStart()
    {
        using var project = TestProject.Create(
            "services.AddOptions<JwtOptions>().BindConfiguration(\"Jwt\").ValidateOnStart();");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task Fires_WhenOnlyOneOfTwoBindingsIsValidated()
    {
        using var project = TestProject.Create(
            "services.AddOptions<A>().Bind(a).ValidateOnStart();\nservices.AddOptions<B>().Bind(b);");

        Assert.NotNull(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_ForConfigureWithALambda()
    {
        using var project = TestProject.Create(
            "services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);");

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }
}
