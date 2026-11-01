using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC002Tests
{
    private readonly SEC002CorsPolicyAllowsUnrestrictedOrigins _rule = new();

    [Fact]
    public async Task Fires_WhenAllowAnyOriginIsUsed()
    {
        using var project = TestProject.Create("policy.AllowAnyOrigin();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC002", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Contains("AllowAnyOrigin() allows every origin (Program.cs:1)", issue.Description);
    }

    [Fact]
    public async Task DoesNotFire_WhenAllowAnyOriginIsAbsent()
    {
        using var project = TestProject.Create("policy.WithOrigins(\"https://example.com\");");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("policy.WithOrigins(\"*\");")]
    [InlineData("policy.WithOrigins(\"https://app.example.com\", \"*\");")]
    public async Task Fires_ForAWildcardInWithOrigins(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("WithOrigins(\"*\") allows every origin", issue!.Description);
    }

    [Theory]
    [InlineData("_ => true")]
    [InlineData("origin => true")]
    [InlineData("(origin) => true")]
    [InlineData("o => { return true; }")]
    [InlineData("delegate { return true; }")]
    [InlineData("delegate (string o) { return true; }")]
    public async Task Fires_AsAnError_ForAnAlwaysTruePredicate(string predicate)
    {
        using var project = TestProject.Create($"policy.SetIsOriginAllowed({predicate}).AllowAnyHeader();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Contains("SetIsOriginAllowed(...) accepts every origin", issue.Description);
        Assert.DoesNotContain("AllowCredentials", issue.Description);
    }

    // ASP.NET refuses AllowAnyOrigin + AllowCredentials at startup; this is the workaround that defeats it.
    [Fact]
    public async Task ExplainsTheCredentialRisk_WhenAllowCredentialsIsPresent()
    {
        using var project = TestProject.Create("""
            builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
                .SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowCredentials()));
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(2, issue!.LineNumber);
        Assert.Contains("with AllowCredentials(), any site can make requests with the visitor's cookies", issue.Description);
    }

    [Theory]
    [InlineData("origin => origin.Contains(\"example.com\")")]
    [InlineData("origin => new Uri(origin).Host.EndsWith(\"example.com\")")]
    public async Task Warns_ForSubstringOriginMatching(string predicate)
    {
        using var project = TestProject.Create($"policy.SetIsOriginAllowed({predicate});");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Contains("matches origins by substring", issue.Description);
    }

    [Theory]
    [InlineData("policy.SetIsOriginAllowed(origin => new Uri(origin).Host.EndsWith(\".example.com\"));")]
    [InlineData("policy.SetIsOriginAllowed(origin => allowedOrigins.Contains(origin));")]
    [InlineData("policy.SetIsOriginAllowed(IsTrustedOrigin);")]
    [InlineData("policy.SetIsOriginAllowed(origin => origin == \"https://app.example.com\");")]
    [InlineData("policy.WithOrigins(settings.AllowedOrigins);")]
    [InlineData("policy.SetIsOriginAllowedToAllowWildcardSubdomains().WithOrigins(\"https://*.example.com\");")]
    [InlineData("// policy.AllowAnyOrigin();")]
    public async Task DoesNotFire_ForAllowListsAndSafeMatching(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_ForAPermissivePolicyLimitedToDevelopment()
    {
        using var project = TestProject.Create("""
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.SetIsOriginAllowed(_ => true).AllowCredentials()));
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ReportsErrorsBeforeWarnings()
    {
        using var project = TestProject.Create("""
            a.SetIsOriginAllowed(o => o.Contains("example.com"));
            b.AllowAnyOrigin();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Equal(2, issue.LineNumber);
        Assert.Contains("matches origins by substring", issue.Description);
    }
}
