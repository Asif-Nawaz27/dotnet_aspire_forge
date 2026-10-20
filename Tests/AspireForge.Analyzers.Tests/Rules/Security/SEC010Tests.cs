using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC010Tests
{
    private readonly SEC010TlsCertificateValidationDisabled _rule = new();

    [Fact]
    public async Task Fires_ForAnAlwaysTrueCallbackOnAnHttpClientHandler_WithFileAndLine()
    {
        using var project = TestProject.Create("""
            builder.Services.AddHttpClient<IPaymentsClient, PaymentsClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                });
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC010", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal(4, issue.LineNumber);
        Assert.Contains("ServerCertificateCustomValidationCallback always returns true", issue.Description);
    }

    [Theory]
    [InlineData("handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;")]
    [InlineData("handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;")]
    [InlineData("handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => { return true; };")]
    [InlineData("handler.ServerCertificateCustomValidationCallback = delegate { return true; };")]
    [InlineData("options.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;")]
    [InlineData("var ssl = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = delegate (object s, X509Certificate c, X509Chain ch, SslPolicyErrors e) { return true; } };")]
    public async Task Fires_AsAnError_ForCallbacksThatAcceptEveryCertificate(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal(Severity.Error, issue!.Severity);
    }

    [Fact]
    public async Task CallsOutTheProcessWideServicePointManagerHook()
    {
        using var project = TestProject.Create(
            "ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("for every connection in the process", issue!.Description);
    }

    [Theory]
    [InlineData("handler.ServerCertificateCustomValidationCallback = (m, c, ch, errors) => errors == SslPolicyErrors.None;")]
    [InlineData("handler.ServerCertificateCustomValidationCallback = (m, cert, ch, e) => cert?.Thumbprint == ExpectedThumbprint;")]
    [InlineData("handler.ServerCertificateCustomValidationCallback = ValidatePinnedCertificate;")]
    [InlineData("if (handler.ServerCertificateCustomValidationCallback == null) { }")]
    [InlineData("// handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;")]
    public async Task DoesNotFire_ForCallbacksThatCheckTheCertificate(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_ForABypassLimitedToDevelopment()
    {
        using var project = TestProject.Create("""
            builder.Services.AddHttpClient("local").ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();

                if (builder.Environment.IsDevelopment())
                {
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                return handler;
            });
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("var cs = \"Server=db;Database=app;TrustServerCertificate=true\";")]
    [InlineData("var cs = \"Server=db;Database=app;Trust Server Certificate=True\";")]
    [InlineData("var cs = \"Host=db;Database=app;Trust Server Certificate=true\";")]
    public async Task Warns_ForTrustServerCertificateInSource(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Contains("TrustServerCertificate=true", issue.Description);
    }

    [Fact]
    public async Task Warns_ForTrustServerCertificateInConfiguration_ButNotTheDevelopmentFile()
    {
        using var project = TestProject.Create();
        File.WriteAllText(
            Path.Combine(project.Context.RootDirectory, "appsettings.Development.json"),
            """{ "ConnectionStrings": { "Db": "Server=localhost;TrustServerCertificate=true" } }""");
        File.WriteAllText(
            Path.Combine(project.Context.RootDirectory, "appsettings.json"),
            "{\n  \"ConnectionStrings\": {\n    \"Db\": \"Server=db;TrustServerCertificate=true\"\n  }\n}");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.EndsWith("appsettings.json", issue.FilePath);
        Assert.Equal(3, issue.LineNumber);
        Assert.DoesNotContain("appsettings.Development.json", issue.Description);
    }

    [Theory]
    [InlineData("var cs = \"Server=db;TrustServerCertificate=false\";")]
    [InlineData("var cs = \"Server=db;Encrypt=true\";")]
    public async Task DoesNotFire_WhenTheDatabaseCertificateIsValidated(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ReportsTheErrorFirst_WhenBothSeveritiesArePresent()
    {
        using var project = TestProject.Create("""
            var cs = "Server=db;TrustServerCertificate=true";
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Equal(2, issue.LineNumber);
        Assert.Contains("TrustServerCertificate=true", issue.Description);
    }
}
