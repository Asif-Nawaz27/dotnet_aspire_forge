using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC013Tests
{
    private readonly SEC013HardcodedSecretsInCode _rule = new();

    [Fact]
    public async Task Fires_ForALiteralSigningKey_WithFileAndLine()
    {
        using var project = TestProject.Create("""
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("my-super-secret-signing-key-123456")),
            };
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC013", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal(3, issue.LineNumber);
        Assert.Contains("literal key in SymmetricSecurityKey (Program.cs:3)", issue.Description);
        Assert.DoesNotContain("my-super-secret-signing-key-123456", issue.Description);
    }

    [Theory]
    [InlineData("var key = new SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(\"abc\"));")]
    [InlineData("var key = new SymmetricSecurityKey(Convert.FromBase64String(\"c2VjcmV0LWtleQ==\"));")]
    [InlineData("using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(\"webhook-secret\"));")]
    [InlineData("var credential = new NetworkCredential(\"svc-user\", \"P@ssw0rd!\");")]
    public async Task Fires_ForLiteralKeyMaterialAndCredentials(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("var cs = \"Host=db;Database=app;Username=app;Password=hunter2\";", "Password=")]
    [InlineData("var cs = \"Server=db;User Id=sa;Pwd=hunter2;\";", "Pwd=")]
    [InlineData("var cs = \"DefaultEndpointsProtocol=https;AccountName=x;AccountKey=abc123==\";", "AccountKey=")]
    [InlineData("var cs = \"Endpoint=sb://x.servicebus.windows.net/;SharedAccessKeyName=root;SharedAccessKey=abc=\";", "SharedAccessKey=")]
    [InlineData("options.UseNpgsql(\"Host=db;Password=hunter2\");", "Password=")]
    public async Task Fires_ForCredentialsInConnectionStringLiterals(string source, string key)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains($"{key} in a connection string literal", issue!.Description);
    }

    [Theory]
    [InlineData("const string ApiKey = \"sk_live_51Habcdefg\";", "ApiKey")]
    [InlineData("private static readonly string ClientSecret = \"0a1b2c3d4e5f\";", "ClientSecret")]
    [InlineData("var smtpPassword = \"correct-horse-battery\";", "smtpPassword")]
    [InlineData("public string GitHubToken { get; set; } = \"ghp_abcdefghijklmnop\";", "GitHubToken")]
    [InlineData("var options = new StripeOptions { ApiKey = \"sk_test_abcdefgh\" };", "ApiKey")]
    [InlineData("Configure(signingKey: \"0123456789abcdef\");", "signingKey")]
    public async Task Fires_ForLiteralsAssignedToSecretNames(string source, string name)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains($"literal assigned to '{name}'", issue!.Description);
    }

    [Theory]
    [InlineData("var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));")]
    [InlineData("var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration[\"Jwt:SigningKey\"]!));")]
    [InlineData("var cs = configuration.GetConnectionString(\"Postgres\");")]
    [InlineData("var cs = $\"Host=db;Password={password}\";")]
    [InlineData("var cs = \"Host=db;Password=;Database=app\";")]
    [InlineData("var cs = \"Host=db;Password=<your-password>\";")]
    [InlineData("var cs = \"Host=db;Database=app;Username=app\";")]
    [InlineData("const string ApiKeyHeader = \"X-Api-Key\";")]
    [InlineData("const string ApiKey = \"X-API-KEY\";")]
    [InlineData("const string SigningKey = \"Jwt:SigningKey\";")]
    [InlineData("var token = \"Bearer\";")]
    [InlineData("var password = \"\";")]
    [InlineData("const string AccessToken = \"https://login.example.com/oauth/token\";")]
    [InlineData("if (request.Token == \"expected-token-value\") { }")]
    [InlineData("public string Token => \"computed-token-value\";")]
    [InlineData("var tokenLifetime = \"00:10:00-not-a-secret\";")]
    [InlineData("// var apiKey = \"sk_live_51Habcdefg\";")]
    public async Task DoesNotFire_ForConfigurationLookupsPlaceholdersAndNonSecrets(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    // Local development defaults - e.g. AspireForge's own generator templates - only work against a
    // database on the developer's machine.
    [Theory]
    [InlineData("var cs = \"Host=localhost;Database=app;Username=postgres;Password=postgres\";")]
    [InlineData("var cs = $\"Host=localhost;Database={name};Username=postgres;Password=postgres\";")]
    [InlineData("var cs = \"Server=127.0.0.1,1433;User Id=sa;Password=Your_password123\";")]
    [InlineData("var cs = \"Server=(localdb)\\\\MSSQLLocalDB;User Id=sa;Password=dev-password\";")]
    [InlineData("var cs = \"Data Source=.;User Id=sa;Password=dev-password\";")]
    public async Task DoesNotFire_ForConnectionStringsToTheLocalMachine(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("var cs = \"Host=localhost.example.com;Password=hunter2\";")]
    [InlineData("var cs = \"Server=prod-db.internal;Password=hunter2\";")]
    public async Task Fires_ForConnectionStringsToOtherHosts(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("private const string ConsoleExporterToken = \"AddConsoleExporter(\";")]
    [InlineData("var resetToken = \"Your token has expired\";")]
    public async Task DoesNotFire_ForCodeSnippetsOrMessagesInSecretNamedVariables(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task CountsALineOnlyOnce()
    {
        using var project = TestProject.Create("var dbPassword = \"Host=db;Password=hunter2-long\";");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.DoesNotContain(",", issue!.Description.Split(':')[1].Split('.')[0]);
    }

    [Fact]
    public async Task ListsAtMostThreeFindings_AndCountsTheRest()
    {
        using var project = TestProject.Create(string.Join(
            "\n",
            Enumerable.Range(1, 5).Select(i => $"const string Service{i}ApiKey = \"key-value-number-{i}\";")));

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Contains("(and 2 more)", issue!.Description);
    }
}
