using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC004Tests
{
    private readonly SEC004SecretsInConfiguration _rule = new();

    [Fact]
    public async Task Fires_ForAPasswordInAConnectionString_WithFileAndLine()
    {
        using var project = TestProject.Create();
        var file = WriteConfig(project, "appsettings.json", """
            {
              "ConnectionStrings": {
                "Postgres": "Host=db;Database=app;Username=app;Password=hunter2"
              }
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC004", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal(file, issue.FilePath);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains("'ConnectionStrings:Postgres' (credential in connection string)", issue.Description);
    }

    [Theory]
    [InlineData("Jwt", "SigningKey")]
    [InlineData("Smtp", "Password")]
    [InlineData("Stripe", "ApiKey")]
    [InlineData("AzureAd", "ClientSecret")]
    [InlineData("GitHub", "AccessToken")]
    public async Task Fires_ForSensitiveKeysWithLiteralValues(string section, string key)
    {
        using var project = TestProject.Create();
        WriteConfig(project, "appsettings.json", $$"""{ "{{section}}": { "{{key}}": "abc123" } }""");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Contains($"'{section}:{key}' (literal value)", issue!.Description);
    }

    [Fact]
    public async Task NeverIncludesTheSecretValueInTheReport()
    {
        using var project = TestProject.Create();
        WriteConfig(project, "appsettings.json", """{ "Jwt": { "SigningKey": "super-secret-value-123" } }""");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.DoesNotContain("super-secret-value-123", issue!.Description);
    }

    [Fact]
    public async Task Fires_ForProductionEnvironmentFiles()
    {
        using var project = TestProject.Create();
        WriteConfig(project, "appsettings.Production.json", """{ "Jwt": { "SigningKey": "abc123" } }""");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Contains("appsettings.Production.json", issue!.Description);
    }

    [Fact]
    public async Task DoesNotFire_ForTheDevelopmentFile()
    {
        using var project = TestProject.Create();
        WriteConfig(project, "appsettings.Development.json", """
            { "ConnectionStrings": { "Postgres": "Host=localhost;Password=postgres" }, "Jwt": { "SigningKey": "dev" } }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("""{ "Jwt": { "SigningKey": "" } }""")]
    [InlineData("""{ "ConnectionStrings": { "Postgres": "Host=db;Database=app;Username=app" } }""")]
    [InlineData("""{ "ConnectionStrings": { "Postgres": "Host=db;Password=;Database=app" } }""")]
    [InlineData("""{ "Jwt": { "SigningKey": "@Microsoft.KeyVault(SecretUri=https://vault.example/secrets/jwt)" } }""")]
    [InlineData("""{ "Jwt": { "SigningKey": "${JWT_SIGNING_KEY}" } }""")]
    [InlineData("""{ "Auth": { "TokenLifetime": "00:10:00", "PasswordPolicy": { "MinLength": 12 } } }""")]
    public async Task DoesNotFire_ForEmptyReferencedOrNonSecretValues(string json)
    {
        using var project = TestProject.Create();
        WriteConfig(project, "appsettings.json", json);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task HandlesCommentsTrailingCommasAndBom()
    {
        using var project = TestProject.Create();
        var path = Path.Combine(project.Context.RootDirectory, "appsettings.json");
        File.WriteAllText(
            path,
            """
            {
              // comment
              "Jwt": { "SigningKey": "abc123", },
            }
            """,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal(3, issue!.LineNumber);
    }

    [Fact]
    public async Task DoesNotFire_ForMalformedJsonWithoutSecrets()
    {
        using var project = TestProject.Create();
        WriteConfig(project, "appsettings.json", """{ "Logging": { "LogLevel": """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenThereAreNoConfigurationFiles()
    {
        using var project = TestProject.Create("var builder = WebApplication.CreateBuilder(args);");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ListsAtMostFiveFindings_AndCountsTheRest()
    {
        using var project = TestProject.Create();
        var keys = Enumerable.Range(1, 7).Select(i => $"\"Key{i}Secret\": \"v{i}\"");
        WriteConfig(project, "appsettings.json", "{ " + string.Join(", ", keys) + " }");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("(and 2 more)", issue!.Description);
        Assert.DoesNotContain("Key6Secret", issue.Description);
    }

    private static string WriteConfig(TestProject project, string fileName, string json)
    {
        var path = Path.Combine(project.Context.RootDirectory, fileName);
        File.WriteAllText(path, json);
        return path;
    }
}
