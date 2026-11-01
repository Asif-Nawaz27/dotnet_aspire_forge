using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC007Tests
{
    private const string Web = "var builder = WebApplication.CreateBuilder(args);\nvar app = builder.Build();\n";

    private readonly SEC007DevelopmentFeaturesInProduction _rule = new();

    [Fact]
    public async Task Fires_AsAnError_ForAnUnguardedDeveloperExceptionPage_WithFileAndLine()
    {
        using var project = TestProject.Create(Web + "app.UseDeveloperExceptionPage();\napp.MapControllers();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC007", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains("UseDeveloperExceptionPage() returns stack traces", issue.Description);
    }

    [Theory]
    [InlineData("app.UseMigrationsEndPoint();")]
    [InlineData("app.UseDatabaseErrorPage();")]
    public async Task Fires_AsAnError_ForEfCoreDevelopmentEndpoints(string call)
    {
        using var project = TestProject.Create(Web + call);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
    }

    [Theory]
    [InlineData("app.MapOpenApi();")]
    [InlineData("app.UseSwagger();")]
    [InlineData("app.UseSwaggerUI(o => o.SwaggerEndpoint(\"/swagger/v1/swagger.json\", \"v1\"));")]
    [InlineData("app.UseOpenApi();\napp.UseSwaggerUi();")]
    [InlineData("app.MapScalarApiReference();")]
    [InlineData("app.UseReDoc();")]
    public async Task Fires_AsAWarning_ForUnguardedApiDocumentation(string call)
    {
        using var project = TestProject.Create(Web + call);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
    }

    // The shapes the ASP.NET Core templates (and this repository's App) use.
    [Theory]
    [InlineData("if (app.Environment.IsDevelopment())\n{\n    app.UseDeveloperExceptionPage();\n    app.MapOpenApi();\n}")]
    [InlineData("if (app.Environment.IsDevelopment())\n    app.UseDeveloperExceptionPage();")]
    [InlineData("if (!app.Environment.IsDevelopment())\n{\n    app.UseExceptionHandler(\"/Error\");\n    app.UseHsts();\n}\nelse\n{\n    app.UseDeveloperExceptionPage();\n    app.UseMigrationsEndPoint();\n}")]
    [InlineData("if (!app.Environment.IsDevelopment())\n    app.UseExceptionHandler(\"/Error\");\nelse\n    app.UseDeveloperExceptionPage();")]
    [InlineData("if (app.Environment.IsDevelopment())\n{\n    app.MapOpenApi();\n}\nelse\n{\n    app.UseHsts();\n}")]
    [InlineData("#if DEBUG\napp.UseDeveloperExceptionPage();\n#endif")]
    public async Task DoesNotFire_InsideADevelopmentGuard(string pipeline)
    {
        using var project = TestProject.Create(Web + pipeline);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    // Keeping API docs out of production - but on in staging - is a deliberate, common choice.
    [Theory]
    [InlineData("if (!app.Environment.IsProduction())\n{\n    app.UseSwagger();\n    app.UseSwaggerUI();\n}")]
    [InlineData("if (app.Environment.IsProduction())\n{\n    app.UseHsts();\n}\nelse\n{\n    app.MapOpenApi();\n}")]
    public async Task DoesNotFire_InsideANonProductionGuard(string pipeline)
    {
        using var project = TestProject.Create(Web + pipeline);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("if (app.Environment.IsDevelopment())\n{\n    app.UseHsts();\n}\nelse\n{\n    app.UseDeveloperExceptionPage();\n}")]
    [InlineData("if (!app.Environment.IsDevelopment())\n{\n    app.UseDeveloperExceptionPage();\n}")]
    [InlineData("if (app.Environment.IsProduction())\n{\n    app.MapOpenApi();\n}")]
    [InlineData("if (app.Environment.IsDevelopment())\n{\n    app.UseHsts();\n}\napp.UseDeveloperExceptionPage();")]
    [InlineData("if (!app.Environment.IsDevelopment())\n    app.UseHsts();\nelse if (useDocs)\n    app.MapOpenApi();")]
    public async Task Fires_WhenTheGuardIsMissingOrPointsTheWrongWay(string pipeline)
    {
        using var project = TestProject.Create(Web + pipeline);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("app.UseExceptionHandler();")]
    [InlineData("builder.Services.AddOpenApi();")]
    [InlineData("builder.Services.AddSwaggerGen();")]
    [InlineData("// app.UseDeveloperExceptionPage();")]
    public async Task DoesNotFire_ForServiceRegistrationsOrProductionSafeCalls(string call)
    {
        using var project = TestProject.Create(Web + call);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_ForNonWebProjects()
    {
        using var project = TestProject.Create("public static class X { public static void Run(IApplicationBuilder app) => app.UseDeveloperExceptionPage(); }");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ReportsTheErrorFirst()
    {
        using var project = TestProject.Create(Web + "app.MapOpenApi();\napp.UseDeveloperExceptionPage();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Equal(4, issue.LineNumber);
        Assert.Contains("MapOpenApi()", issue.Description);
    }
}
