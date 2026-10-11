using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC008Tests
{
    private const string AuthenticatedApp = """
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        """;

    private readonly SEC008AuthorizationNotEnforced _rule = new();

    [Fact]
    public async Task Fires_WhenAuthenticationIsConfiguredButNothingRequiresIt()
    {
        using var project = TestProject.Create(AuthenticatedApp + """
            app.MapGet("/orders", () => "all orders");
            app.MapControllers();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC008", issue!.RuleId);
        Assert.Equal(Severity.Warning, issue.Severity);
        Assert.Contains("Every endpoint accepts anonymous requests", issue.Description);
    }

    [Theory]
    [InlineData("[Authorize]\npublic class OrdersController : ControllerBase { }")]
    [InlineData("[Authorize(Roles = \"Admin\")]\npublic class AdminController : ControllerBase { }")]
    [InlineData("[ApiController, Authorize]\npublic class OrdersController : ControllerBase { }")]
    [InlineData("[Authorize, Route(\"api/orders\")]\npublic class OrdersController : ControllerBase { }")]
    [InlineData("[Microsoft.AspNetCore.Authorization.Authorize]\npublic class OrdersController : ControllerBase { }")]
    [InlineData("[AuthorizeAttribute]\npublic class OrdersController : ControllerBase { }")]
    [InlineData("public class OrdersController : ControllerBase\n{\n    [HttpGet, Authorize(Policy = \"Read\")]\n    public IActionResult Get() => Ok();\n}")]
    public async Task DoesNotFire_ForTheAuthorizeAttribute(string controller)
    {
        using var project = TestProject.Create(AuthenticatedApp + controller);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("app.MapGet(\"/secure\", () => \"hi\").RequireAuthorization();")]
    [InlineData("app.MapGroup(\"/api\").RequireAuthorization(\"ApiPolicy\");")]
    [InlineData("app.MapControllers().RequireAuthorization();")]
    [InlineData("builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());")]
    [InlineData("builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());")]
    [InlineData("builder.Services.AddControllers(o => o.Filters.Add(new AuthorizeFilter()));")]
    [InlineData("builder.Services.AddRazorPages(o => o.Conventions.AuthorizeFolder(\"/Admin\"));")]
    public async Task DoesNotFire_ForEndpointOrGlobalAuthorization(string enforcement)
    {
        using var project = TestProject.Create(AuthenticatedApp + enforcement);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("[AllowAnonymous]\npublic class PublicController : ControllerBase { }")]
    [InlineData("[AuthorizeView]\npublic class Widget { }")]
    [InlineData("// [Authorize]\npublic class OrdersController : ControllerBase { }")]
    [InlineData("/* app.MapGet(\"/x\", () => 1).RequireAuthorization(); */")]
    public async Task Fires_WhenTheOnlyMatchIsNotARealRequirement(string source)
    {
        using var project = TestProject.Create(AuthenticatedApp + source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenAuthenticationIsNotConfigured_BecauseSEC001CoversIt()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);\napp.MapGet(\"/\", () => 1);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_ForNonWebProjects()
    {
        using var project = TestProject.Create("services.AddAuthentication();\npublic class Worker { }");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
