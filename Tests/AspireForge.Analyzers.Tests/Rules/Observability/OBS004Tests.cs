using AspireForge.Analyzers.Rules.Observability;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Observability;

public class OBS004Tests
{
    private readonly OBS004SensitiveDataLoggingEnabled _rule = new();

    [Fact]
    public async Task Fires_ForAnUnguardedCall_WithFileAndLine()
    {
        using var project = TestProject.Create("""
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString)
                    .EnableSensitiveDataLogging());
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
        Assert.Equal("OBS004", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.EndsWith("Program.cs", issue.FilePath);
        Assert.Equal(4, issue.LineNumber);
        Assert.Contains("Program.cs:4", issue.Description);
    }

    [Theory]
    [InlineData("options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());")]
    [InlineData("options.EnableSensitiveDataLogging(env.IsEnvironment(\"Development\"));")]
    [InlineData("options.EnableSensitiveDataLogging(false);")]
    public async Task DoesNotFire_WhenTheArgumentLimitsItToDevelopment(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("options.EnableSensitiveDataLogging(true);")]
    [InlineData("options.EnableSensitiveDataLogging(!builder.Environment.IsDevelopment());")]
    public async Task Fires_WhenTheArgumentDoesNotLimitIt(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Fact]
    public async Task DoesNotFire_InsideABracedDevelopmentIf_EvenWithinALambda()
    {
        using var project = TestProject.Create("""
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(connectionString);

                if (builder.Environment.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                }
            });
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_InsideAnOuterDevelopmentIf()
    {
        using var project = TestProject.Create("""
            if (app.Environment.IsDevelopment())
            {
                builder.Services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseNpgsql(connectionString).EnableSensitiveDataLogging();
                });
            }
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_InABracelessDevelopmentIf()
    {
        using var project = TestProject.Create("""
            if (env.IsDevelopment())
                options.EnableSensitiveDataLogging();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_InsideIfDebug()
    {
        using var project = TestProject.Create("""
            #if DEBUG
                options.EnableSensitiveDataLogging();
            #endif
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Theory]
    [InlineData("""
        if (!env.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
        """)]
    [InlineData("""
        if (env.IsDevelopment())
        {
            options.LogTo(Console.WriteLine);
        }
        else
        {
            options.EnableSensitiveDataLogging();
        }
        """)]
    [InlineData("""
        if (env.IsDevelopment())
        {
            options.LogTo(Console.WriteLine);
        }
        options.EnableSensitiveDataLogging();
        """)]
    [InlineData("""
        #if DEBUG
            options.LogTo(Console.WriteLine);
        #endif
            options.EnableSensitiveDataLogging();
        """)]
    [InlineData("""
        #if DEBUG
            options.LogTo(Console.WriteLine);
        #else
            options.EnableSensitiveDataLogging();
        #endif
        """)]
    public async Task Fires_WhenTheGuardDoesNotCoverTheCall(string source)
    {
        using var project = TestProject.Create(source);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.NotNull(issue);
    }

    [Fact]
    public async Task ReportsTheRightLine_AfterAMultiLineBlockComment()
    {
        using var project = TestProject.Create("""
            /*
             * options.EnableSensitiveDataLogging();
             */
            options.EnableSensitiveDataLogging();
            """);

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Equal(4, issue!.LineNumber);
    }

    [Fact]
    public async Task DoesNotFire_WhenTheCallIsOnlyInAComment()
    {
        using var project = TestProject.Create("// options.EnableSensitiveDataLogging();");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }

    [Fact]
    public async Task DoesNotFire_WhenSensitiveDataLoggingIsNotUsed()
    {
        using var project = TestProject.Create("builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs));");

        var issue = await _rule.EvaluateAsync(project.Context);

        Assert.Null(issue);
    }
}
