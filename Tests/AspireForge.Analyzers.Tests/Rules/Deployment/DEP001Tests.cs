using AspireForge.Analyzers.Rules.Deployment;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests.Rules.Deployment;

// Every test pins "today" so results don't change as real dates pass the lifecycle table.
public class DEP001Tests
{
    private static readonly DateOnly Today = new(2026, 10, 4);

    [Theory]
    [InlineData("net6.0", "2024-11-12")]
    [InlineData("net7.0", "2024-05-14")]
    [InlineData("net5.0", "2022-05-10")]
    [InlineData("netcoreapp3.1", "2022-12-13")]
    [InlineData("net461", "2022-04-26")]
    public async Task Fires_AsAnError_ForOutOfSupportFrameworks(string framework, string endDate)
    {
        var issue = await Evaluate(Today, framework);

        Assert.NotNull(issue);
        Assert.Equal("DEP001", issue!.RuleId);
        Assert.Equal(Severity.Error, issue.Severity);
        Assert.Equal("Unsupported .NET version", issue.Title);
        Assert.Contains($"{framework} (support ended {endDate}) is out of support and no longer receives", issue.Description);
    }

    [Fact]
    public async Task UsesPluralWording_ForSeveralOutOfSupportFrameworks()
    {
        var issue = await Evaluate(Today, "net6.0", "net7.0");

        Assert.Contains("are out of support and no longer receive security patches", issue!.Description);
    }

    [Fact]
    public async Task Warns_ForAFrameworkEndingWithinSixMonths()
    {
        var issue = await Evaluate(Today, "net8.0");

        Assert.NotNull(issue);
        Assert.Equal(Severity.Warning, issue!.Severity);
        Assert.Equal(".NET version nearing end of support", issue.Title);
        Assert.Contains("net8.0 (support ends 2026-11-10)", issue.Description);
    }

    [Fact]
    public async Task DoesNotWarn_BeforeTheSixMonthWindowOpens()
    {
        var issue = await Evaluate(new DateOnly(2026, 4, 1), "net8.0");

        Assert.Null(issue);
    }

    [Fact]
    public async Task BecomesAnError_OnTheEndOfSupportDate()
    {
        var issue = await Evaluate(new DateOnly(2026, 11, 10), "net9.0");

        Assert.Equal(Severity.Error, issue!.Severity);
    }

    [Theory]
    [InlineData("net10.0")]
    [InlineData("net11.0")]
    [InlineData("net48")]
    [InlineData("net462")]
    [InlineData("netstandard2.0")]
    public async Task DoesNotFire_ForSupportedOrUnknownFrameworks(string framework)
    {
        var issue = await Evaluate(Today, framework);

        Assert.Null(issue);
    }

    [Fact]
    public async Task UsesTheBaseFrameworkLifecycle_ForPlatformSpecificTargets()
    {
        var issue = await Evaluate(Today, "net6.0-windows10.0.19041.0");

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Contains("net6.0-windows10.0.19041.0", issue.Description);
    }

    [Fact]
    public async Task ReportsEveryAffectedFramework_WhenMultiTargeting_AsAnError()
    {
        var issue = await Evaluate(Today, "net10.0", "net8.0", "net6.0");

        Assert.Equal(Severity.Error, issue!.Severity);
        Assert.Contains("net6.0 (support ended 2024-11-12) is out of support", issue.Description);
        Assert.Contains("net8.0 (support ends 2026-11-10) reaches end of support soon", issue.Description);
        Assert.DoesNotContain("net10.0 (", issue.Description);
    }

    [Fact]
    public async Task DoesNotFire_WhenTheFrameworkIsUnknown()
    {
        var issue = await Evaluate(Today);

        Assert.Null(issue);
    }

    private static Task<AnalysisIssue?> Evaluate(DateOnly today, params string[] frameworks)
    {
        var context = new ProjectContext
        {
            RootDirectory = Path.GetTempPath(),
            ProjectFile = Path.Combine(Path.GetTempPath(), "Api.csproj"),
            ProjectName = "Api",
            TargetFramework = frameworks.FirstOrDefault(),
            TargetFrameworks = frameworks,
            SourceFiles = [],
            ProjectReferences = [],
            PackageReferences = [],
        };

        return new DEP001UnsupportedTargetFramework(today).EvaluateAsync(context);
    }
}
