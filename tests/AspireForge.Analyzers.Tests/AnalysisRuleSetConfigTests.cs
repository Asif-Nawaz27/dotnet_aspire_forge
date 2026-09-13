using AspireForge.Core.Analysis;
using AspireForge.Core.Configuration;

namespace AspireForge.Analyzers.Tests;

public class AnalysisRuleSetConfigTests
{
    [Fact]
    public void ApplyConfig_RemovesRulesExplicitlyDisabled()
    {
        var rules = AnalysisRuleSet.CreateDefault();
        var config = new AspireForgeConfig
        {
            Rules = new Dictionary<string, RuleConfig> { ["TEST002"] = new() { Enabled = false } },
        };

        var filtered = AnalysisRuleSet.ApplyConfig(rules, config);

        Assert.DoesNotContain(filtered, rule => rule.Id == "TEST002");
        Assert.Equal(rules.Count - 1, filtered.Count);
    }

    [Fact]
    public void ApplyConfig_KeepsRulesNotMentionedInConfig()
    {
        var rules = AnalysisRuleSet.CreateDefault();
        var config = new AspireForgeConfig
        {
            Rules = new Dictionary<string, RuleConfig> { ["TEST002"] = new() { Enabled = false } },
        };

        var filtered = AnalysisRuleSet.ApplyConfig(rules, config);

        Assert.Contains(filtered, rule => rule.Id == "SEC001");
    }

    [Fact]
    public void ApplySeverityOverrides_ReplacesSeverity_ForMatchingRuleId()
    {
        var result = new AnalysisResult("Sample", [new AnalysisIssue("SEC001", "Title", "Description", Severity.Warning)]);
        var config = new AspireForgeConfig
        {
            Rules = new Dictionary<string, RuleConfig> { ["SEC001"] = new() { Severity = Severity.Error } },
        };

        var adjusted = AnalysisRuleSet.ApplySeverityOverrides(result, config);

        Assert.Equal(Severity.Error, adjusted.Issues.Single().Severity);
    }

    [Fact]
    public void ApplySeverityOverrides_LeavesUnmentionedIssuesUnchanged()
    {
        var result = new AnalysisResult("Sample", [new AnalysisIssue("SEC001", "Title", "Description", Severity.Warning)]);
        var config = new AspireForgeConfig();

        var adjusted = AnalysisRuleSet.ApplySeverityOverrides(result, config);

        Assert.Equal(Severity.Warning, adjusted.Issues.Single().Severity);
    }
}
