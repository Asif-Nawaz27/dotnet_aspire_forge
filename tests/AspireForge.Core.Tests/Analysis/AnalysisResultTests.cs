using AspireForge.Core.Analysis;

namespace AspireForge.Core.Tests.Analysis;

public class AnalysisResultTests
{
    [Fact]
    public void HasErrors_IsFalse_WhenNoIssues()
    {
        var result = new AnalysisResult("Sample", []);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void HasErrors_IsFalse_WhenOnlyWarnings()
    {
        var result = new AnalysisResult("Sample", [Issue(Severity.Warning), Issue(Severity.Info)]);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void HasErrors_IsTrue_WhenAnErrorIssueIsPresent()
    {
        var result = new AnalysisResult("Sample", [Issue(Severity.Warning), Issue(Severity.Error)]);

        Assert.True(result.HasErrors);
    }

    private static AnalysisIssue Issue(Severity severity) =>
        new("RULE", "Title", "Description", severity);
}
