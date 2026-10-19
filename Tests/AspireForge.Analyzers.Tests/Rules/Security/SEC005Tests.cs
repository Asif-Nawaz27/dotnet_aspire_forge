using AspireForge.Analyzers.Rules.Security;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Security;

public class SEC005Tests
{
    private readonly SEC005WeakJwtValidation _rule = new();

    // The shape of this repository's App - every check on, a short skew.
    [Fact]
    public async Task DoesNotFire_ForStrictValidation()
    {
        using var project = TestProject.Create("""
            bearer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("RequireSignedTokens", "unsigned")]
    [InlineData("ValidateLifetime", "expired tokens are accepted forever")]
    public async Task Fires_AsAnError_WhenAuthenticityOrExpiryChecksAreOff(string setting, string consequence)
    {
        using var project = TestProject.Create($$"""
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                {{setting}} = false,
            };
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("SEC005", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal(4, issue.LineNumber);
        Assert.Contains($"{setting} = false - {consequence}", issue.Description);
    }

    [Theory]
    [InlineData("ValidateIssuer")]
    [InlineData("ValidateAudience")]
    [InlineData("RequireExpirationTime")]
    [InlineData("RequireHttpsMetadata")]
    public async Task Fires_AsAWarning_ForLooserChecks(string setting)
    {
        using var project = TestProject.Create($"options.{setting} = false;");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal(Severity.Warning, issue!.Severity);
    }

    [Theory]
    [InlineData("SignatureValidator = (token, parameters) => new JwtSecurityToken(token),")]
    [InlineData("SignatureValidator = delegate (string token, TokenValidationParameters p) { return new JsonWebToken(token); },")]
    public async Task Fires_AsAnError_ForASignatureValidatorBypass(string validator)
    {
        using var project = TestProject.Create($$"""
            options.TokenValidationParameters = new TokenValidationParameters
            {
                {{validator}}
            };
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Contains("anyone can mint a valid token", issue.Description);
    }

    [Theory]
    [InlineData("TimeSpan.MaxValue")]
    [InlineData("TimeSpan.FromDays(1)")]
    [InlineData("TimeSpan.FromHours(2)")]
    [InlineData("TimeSpan.FromMinutes(30)")]
    [InlineData("TimeSpan.FromSeconds(600)")]
    [InlineData("new TimeSpan(0, 10, 0)")]
    public async Task Fires_ForAClockSkewAboveFiveMinutes(string skew)
    {
        using var project = TestProject.Create($"options.TokenValidationParameters = new() {{ ClockSkew = {skew} }};");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Contains($"ClockSkew = {skew}", issue.Description);
    }

    [Theory]
    [InlineData("TimeSpan.Zero")]
    [InlineData("TimeSpan.FromMinutes(5)")]
    [InlineData("TimeSpan.FromSeconds(30)")]
    [InlineData("new TimeSpan(0, 2, 0)")]
    [InlineData("configuredSkew")]
    public async Task DoesNotFire_ForAClockSkewOfFiveMinutesOrLess_OrUnknown(string skew)
    {
        using var project = TestProject.Create($"options.TokenValidationParameters = new() {{ ClockSkew = {skew} }};");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_ForRelaxedSettingsLimitedToDevelopment()
    {
        using var project = TestProject.Create("""
            builder.Services.AddAuthentication().AddJwtBearer(options =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateLifetime = false,
                    };
                }
            });

            #if DEBUG
            options.ValidateAudience = false;
            #endif
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("if (!builder.Environment.IsDevelopment()) { options.RequireHttpsMetadata = false; }")]
    [InlineData("if (env.IsProduction()) { options.ValidateLifetime = false; }")]
    public async Task Fires_WhenTheGuardIsNotDevelopmentOnly(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Theory]
    [InlineData("ValidateIssuerSigningKey = false,")]
    [InlineData("if (parameters.ValidateLifetime == false) { }")]
    [InlineData("options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();")]
    [InlineData("// ValidateLifetime = false,")]
    [InlineData("SignatureValidator = (token, p) => handler.ValidateSignature(token, p),")]
    public async Task DoesNotFire_ForSafeOrNonLiteralSettings(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task ReportsTheMostSevereFindingFirst()
    {
        using var project = TestProject.Create("""
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateLifetime = false,
            };
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Equal(5, issue.LineNumber);
        Assert.StartsWith("JWT validation is weakened: ValidateLifetime = false", issue.Description);
        Assert.Contains("ValidateAudience = false", issue.Description);
        Assert.Contains("RequireHttpsMetadata = false", issue.Description);
    }
}
