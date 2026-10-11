using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC009Tests
{
    private readonly SEC009MiddlewareOrder _rule = new();

    // The order the ASP.NET Core docs recommend - and the order this repository's App uses.
    [Fact]
    public async Task DoesNotFire_ForTheDocumentedOrder()
    {
        using var project = TestProject.Create("""
            var app = builder.Build();
            app.UseExceptionHandler();
            app.UseHsts();
            app.UseHttpsRedirection();
            app.UseResponseCompression();
            app.UseRouting();
            app.UseCors("Frontend");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapControllers();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task Fires_AsAnError_WhenAuthorizationComesBeforeAuthentication()
    {
        using var project = TestProject.Create("""
            var app = builder.Build();
            app.UseAuthorization();
            app.UseAuthentication();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC009", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal(2, issue.LineNumber);
        Assert.Contains("UseAuthorization() is called before UseAuthentication() (Program.cs:2)", issue.Description);
    }

    [Fact]
    public async Task Fires_AsAnError_WhenAuthorizationComesBeforeAnExplicitUseRouting()
    {
        using var project = TestProject.Create("app.UseAuthorization();\napp.UseRouting();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Contains("can't see the endpoint's [Authorize]", issue.Description);
    }

    [Theory]
    [InlineData("app.UseAuthorization();\napp.UseCors();", "preflight")]
    [InlineData("app.UseCors();\napp.UseRouting();", "[EnableCors]")]
    [InlineData("app.UseRateLimiter();\napp.UseRouting();", "[EnableRateLimiting]")]
    [InlineData("app.UseAuthentication();\napp.UseExceptionHandler(\"/error\");", "bypass the error handler")]
    [InlineData("app.UseResponseCompression();\napp.UseDeveloperExceptionPage();", "bypass the error handler")]
    public async Task Fires_AsAWarning_ForOtherOrderingMistakes(string source, string consequence)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Contains(consequence, issue.Description);
    }

    [Fact]
    public async Task UsesTheMostSevereViolation_ForSeverityAndLocation()
    {
        using var project = TestProject.Create("""
            app.UseAuthorization();
            app.UseCors();
            app.UseAuthentication();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.StartsWith("UseAuthorization() is called before UseAuthentication()", issue.Description);
        Assert.Contains("UseAuthorization() is called before UseCors()", issue.Description);
    }

    // Without an explicit UseRouting(), WebApplication adds routing first - nothing to compare against.
    [Fact]
    public async Task IgnoresRoutingPairs_WhenUseRoutingIsImplicit()
    {
        using var project = TestProject.Create("app.UseRateLimiter();\napp.UseCors();\napp.UseAuthorization();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task OnlyComparesCallsInTheSameFile()
    {
        using var project = TestProject.Create("app.UseAuthorization();");
        var other = Path.Combine(project.Context.RootDirectory, "Pipeline.cs");
        File.WriteAllText(other, "app.UseAuthentication();");
        var context = new ProjectContext
        {
            RootDirectory = project.Context.RootDirectory,
            ProjectFile = project.Context.ProjectFile,
            ProjectName = project.Context.ProjectName,
            SourceFiles = [.. project.Context.SourceFiles, other],
            ProjectReferences = [],
            PackageReferences = [],
        };

        var issue = await _rule.EvaluateAsync(context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("// app.UseAuthorization();\napp.UseAuthentication();\napp.UseAuthorization();")]
    [InlineData("services.AddAuthorization();\napp.UseAuthentication();\napp.UseAuthorization();")]
    public async Task IgnoresCommentsAndServiceRegistrations(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenOnlyOneSideOfAPairIsPresent()
    {
        using var project = TestProject.Create("app.UseAuthorization();\napp.MapControllers();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
