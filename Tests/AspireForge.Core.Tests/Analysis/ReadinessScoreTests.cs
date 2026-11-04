using AspireForge.Core.Analysis;

namespace AspireForge.Core.Tests.Analysis;

public class ReadinessScoreTests
{
    [Fact]
    public void IsPerfect_WithNoFindings()
    {
        var score = ReadinessScore.Calculate([]);

        Assert.Equal(100, score.Points);
        Assert.Equal(10, score.OutOfTen);
        Assert.Null(score.CapReason);
    }

    [Theory]
    [InlineData(Severity.Warning, 95)]
    [InlineData(Severity.Suggestion, 98)]
    [InlineData(Severity.Info, 99)]
    public void SubtractsAWeightedPenaltyPerFinding(Severity severity, int expected)
    {
        var score = ReadinessScore.Calculate([Issue(severity)]);

        Assert.Equal(expected, score.Points);
        Assert.Equal(100 - expected, score.Penalty);
    }

    // 10/10 means nothing to fix: even one info finding drops it to 9.
    [Fact]
    public void RoundsDownOutOfTen()
    {
        Assert.Equal(9, ReadinessScore.Calculate([Issue(Severity.Info)]).OutOfTen);
        Assert.Equal(6, ReadinessScore.Calculate([.. Enumerable.Repeat(Issue(Severity.Warning), 7)]).OutOfTen);
    }

    [Fact]
    public void CapsAt60_WhenAnyErrorIsPresent()
    {
        var score = ReadinessScore.Calculate([Issue(Severity.Error)]);

        Assert.Equal(60, score.Points);
        Assert.Equal(6, score.OutOfTen);
        Assert.Equal(15, score.Penalty);
        Assert.Equal("capped at 60 by 1 error", score.CapReason);
    }

    [Fact]
    public void CapsAt30_WhenAnyCriticalIsPresent_AndCountsThemInTheReason()
    {
        var score = ReadinessScore.Calculate([Issue(Severity.Critical), Issue(Severity.Critical)]);

        Assert.Equal(30, score.Points);
        Assert.Equal("capped at 30 by 2 critical findings", score.CapReason);
    }

    [Fact]
    public void ReportsNoCap_WhenThePenaltyAlreadyGoesBelowIt()
    {
        var issues = Enumerable.Repeat(Issue(Severity.Error), 4).ToList();

        var score = ReadinessScore.Calculate(issues);

        Assert.Equal(40, score.Points);
        Assert.Null(score.CapReason);
    }

    [Fact]
    public void NeverGoesBelowZero()
    {
        var score = ReadinessScore.Calculate([.. Enumerable.Repeat(Issue(Severity.Critical), 5)]);

        Assert.Equal(0, score.Points);
        Assert.Equal(150, score.Penalty);
    }

    // The old pass-ratio score rose as rules were added; this one only looks at findings.
    [Fact]
    public void DependsOnlyOnFindings_NotOnHowManyRulesPassed()
    {
        var sixWarnings = Enumerable.Repeat(Issue(Severity.Warning), 6).ToList();

        Assert.Equal(70, ReadinessScore.Calculate(sixWarnings).Points);
    }

    private static AnalysisIssue Issue(Severity severity) => new("X001", "Title", "Description", severity);
}
