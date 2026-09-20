using AspireForge.Analyzers.Rules.Architecture;

namespace AspireForge.Analyzers.Tests.Rules.Architecture;

//Special test
public class ARCH001Tests
{
    private readonly ARCH001ApiDirectlyAccessesPersistenceLayer _rule = new();

    [Fact]
    public async Task Fires_WhenWebProjectDirectlyReferencesAndUsesEfCore()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);\nbuilder.Services.AddDbContext<AppDbContext>();",
            packageReferences: ["Microsoft.EntityFrameworkCore.Design"]);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("ARCH001", issue!.RuleId);
    }

    [Fact]
    public async Task DoesNotFire_WhenPackageIsReferencedButNotUsedInOwnCode()
    {
        // e.g. AspireForge's own `add postgres` pins Microsoft.EntityFrameworkCore on the Api project
        // purely to keep its version in sync with Infrastructure's - not a real violation.
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);",
            packageReferences: ["Microsoft.EntityFrameworkCore.Design"]);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenNoPersistencePackageIsReferenced()
    {
        using var project = TestProject.Create(
            "var builder = WebApplication.CreateBuilder(args);", packageReferences: ["Swashbuckle.AspNetCore"]);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenNotAWebProject()
    {
        using var project = TestProject.Create(
            "public class Repository { }", packageReferences: ["Npgsql.EntityFrameworkCore.PostgreSQL"]);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
