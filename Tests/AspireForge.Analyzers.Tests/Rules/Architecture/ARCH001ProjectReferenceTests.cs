using AspireForge.Analyzers.Rules.Architecture;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Architecture;

// The clean-architecture case: the Api gets EF Core through its Infrastructure project reference, so it
// has no persistence package of its own - its code using the DbContext is still a violation.
public class ARCH001ProjectReferenceTests
{
    private const string Web = "var builder = WebApplication.CreateBuilder(args);\n";

    private readonly ARCH001ApiDirectlyAccessesPersistenceLayer _rule = new();

    [Fact]
    public async Task Fires_ForAControllerInjectingTheDbContext_WithoutAnyPersistencePackage()
    {
        using var project = TestProject.Create(Web);
        var controller = WriteOwnFile(project, "OrdersController.cs", """
            public class OrdersController(AppDbContext db) : ControllerBase
            {
                public IActionResult Get() => Ok(db.Orders.ToList());
            }
            """);

        var issue = await _rule.EvaluateAsync(With(project, controller));

        Assert.NotNull(issue);
        Assert.Equal("ARCH001", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Equal(controller, issue.FilePath);
        Assert.Equal(1, issue.LineNumber);
        Assert.Contains("AppDbContext injected or declared (OrdersController.cs:1)", issue.Description);
    }

    [Theory]
    [InlineData("public class OrdersController : ControllerBase\n{\n    private readonly AppDbContext _db;\n}", "AppDbContext injected or declared")]
    [InlineData("public OrdersController(App.Infrastructure.Persistence.AppDbContext db) { }", "AppDbContext injected or declared")]
    [InlineData("public class Handler(IDbContextFactory<AppDbContext> factory) { }", "IDbContextFactory<...> used")]
    [InlineData("public IQueryable<Order> Orders(DbSet<Order> set) => set;", "DbSet<...> used")]
    [InlineData("var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();", "AppDbContext resolved from the service provider")]
    [InlineData("public class ApiDbContext : DbContext { }", "ApiDbContext derives from DbContext")]
    [InlineData("public class AuthDbContext : IdentityDbContext<User> { }", "AuthDbContext derives from DbContext")]
    public async Task Fires_ForOtherDirectDbContextAccess(string source, string expected)
    {
        using var project = TestProject.Create(Web);
        var file = WriteOwnFile(project, "Endpoints.cs", source);

        var issue = await _rule.EvaluateAsync(With(project, file));

        Assert.Contains(expected, issue!.Description);
    }

    [Fact]
    public async Task Fires_ForAMinimalApiEndpointInProgramCs()
    {
        using var project = TestProject.Create(Web + "app.MapGet(\"/orders\", async (AppDbContext db) => await db.Orders.ToListAsync());");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("AppDbContext injected or declared (Program.cs:2)", issue!.Description);
    }

    // The composition root may register and resolve the context for startup work.
    [Theory]
    [InlineData("builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs));")]
    [InlineData("builder.Services.AddDbContextFactory<AppDbContext>();")]
    [InlineData("using (var scope = app.Services.CreateScope())\n{\n    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();\n    db.Database.Migrate();\n}")]
    [InlineData("using var scope = app.Services.CreateScope();\nAppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();\nawait db.Database.MigrateAsync();")]
    [InlineData("builder.Services.AddInfrastructure(builder.Configuration);\nawait app.Services.ApplyMigrationsAsync();")]
    public async Task DoesNotFire_ForTheCompositionRoot(string program)
    {
        using var project = TestProject.Create(Web + program);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("public class OrdersController(IOrderService orders) : ControllerBase { }")]
    [InlineData("public void Configure(DbContextOptionsBuilder<AppDbContext> options) { }")]
    [InlineData("var type = typeof(AppDbContext);")]
    [InlineData("// public OrdersController(AppDbContext db) { }")]
    [InlineData("var message = \"Use AppDbContext db inside Infrastructure\";")]
    public async Task DoesNotFire_ForServicesOptionsOrMentions(string source)
    {
        using var project = TestProject.Create(Web);
        var file = WriteOwnFile(project, "Other.cs", source);

        var issue = await _rule.EvaluateAsync(With(project, file));

        Assert.Null(issue);
    }

    // The DbContext living in Infrastructure, used there, is exactly where it belongs.
    [Fact]
    public async Task IgnoresReferencedProjectFiles()
    {
        using var project = TestProject.Create(Web);
        var infrastructure = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(infrastructure);
        var repository = Path.Combine(infrastructure, "OrderRepository.cs");
        File.WriteAllText(repository, "internal sealed class OrderRepository(AppDbContext db) { }");

        try
        {
            var issue = await _rule.EvaluateAsync(With(project, repository));

            Assert.Null(issue);
        }
        finally
        {
            Directory.Delete(infrastructure, recursive: true);
        }
    }

    [Fact]
    public async Task DoesNotFire_ForNonWebProjects()
    {
        using var project = TestProject.Create("public class Worker(AppDbContext db) { }");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    private static string WriteOwnFile(TestProject project, string name, string source)
    {
        var path = Path.Combine(project.Context.RootDirectory, name);
        File.WriteAllText(path, source);
        return path;
    }

    private static ProjectContext With(TestProject project, string extraFile) => new()
    {
        RootDirectory = project.Context.RootDirectory,
        ProjectFile = project.Context.ProjectFile,
        ProjectName = project.Context.ProjectName,
        SourceFiles = [.. project.Context.SourceFiles, extraFile],
        ProjectReferences = [],
        PackageReferences = [],
    };
}
