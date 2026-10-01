using AspireForge.Analyzers.Rules.Reliability;

namespace AspireForge.Analyzers.Tests.Rules.Reliability;

public class REL004Tests
{
    private const string WebApp = "var builder = WebApplication.CreateBuilder(args);\n";

    private readonly REL004HealthChecksIgnoreDependencies _rule = new();

    [Fact]
    public async Task Fires_WhenDatabasePackageHasNoDependencyHealthCheck()
    {
        using var project = TestProject.Create(
            WebApp + "builder.Services.AddHealthChecks();",
            packageReferences: ["Npgsql.EntityFrameworkCore.PostgreSQL"]);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("REL004", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenDbContextCheckIsRegistered()
    {
        using var project = TestProject.Create(
            WebApp + "builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();",
            packageReferences: ["Microsoft.EntityFrameworkCore"]);

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task Fires_WhenOnlyRedisCacheIsRegistered()
    {
        using var project = TestProject.Create(
            WebApp + "builder.Services.AddRedisDistributedCache();",
            packageReferences: ["StackExchange.Redis"]);

        Assert.NotNull(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenRedisHealthCheckIsRegistered()
    {
        using var project = TestProject.Create(
            WebApp + "builder.Services.AddHealthChecks().AddRedis(connectionString);",
            packageReferences: ["StackExchange.Redis"]);

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenAspireIntegrationIsUsed()
    {
        using var project = TestProject.Create(
            WebApp,
            packageReferences: ["Aspire.Npgsql.EntityFrameworkCore.PostgreSQL", "Microsoft.EntityFrameworkCore"]);

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenNoDependencyPackageIsReferenced()
    {
        using var project = TestProject.Create(WebApp);

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }

    [Fact]
    public async Task DoesNotFire_WhenNotAWebProject()
    {
        using var project = TestProject.Create("var x = 1;", packageReferences: ["Npgsql"]);

        Assert.Null(await _rule.EvaluateAsync(project.Context));
    }
}
