using System.Text;
using AspireForge.Analyzers.Suppressions;
using AspireForge.Cli.Commands;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Output;

// The report as GitHub-flavored Markdown, for a CI job summary or a pull request comment: the score and
// totals, a table of findings, the detail behind a fold so a long report stays readable, and what was
// accepted - so a green build can't hide that findings were left out.
public static class MarkdownReportBuilder
{
    public static string Build(
        ProjectContext context,
        AnalysisResult result,
        IReadOnlyList<IAnalysisRule> rules,
        IReadOnlyList<AcceptedFinding> accepted,
        int unmatchedBaselineEntries,
        IReadOnlySet<string> fixableRuleIds,
        string currentDirectory,
        string architecture,
        IReadOnlySet<string> skippedRuleIds)
    {
        var report = new StringBuilder();
        var score = ReadinessScore.Calculate(result.Issues);
        var reason = score.CapReason is not null ? $" - {score.CapReason}" : string.Empty;

        report.AppendLine($"## AspireForge Doctor: {Escape(context.ProjectName)}");
        report.AppendLine();
        report.AppendLine($"**Production Readiness: {score.OutOfTen}/10 ({score.Points}/100)**{reason}");
        report.AppendLine();
        report.AppendLine($"Architecture: {architecture}");
        report.AppendLine();

        WriteTotals(report, result, rules, accepted, skippedRuleIds);

        if (skippedRuleIds.Count > 0)
        {
            report.AppendLine($"> {string.Join(" and ", skippedRuleIds.Order())} skipped: the architecture isn't recognised. "
                + "Set `\"architecture\"` to `\"clean\"` or `\"layered\"` in `.aspireforge/config.json` to check layer dependencies.");
            report.AppendLine();
        }
        WriteNotes(report, accepted, unmatchedBaselineEntries);

        if (result.Issues.Count == 0)
        {
            report.AppendLine("No findings.");
            report.AppendLine();
        }
        else
        {
            WriteFindings(report, context, result, rules, fixableRuleIds, currentDirectory);
        }

        if (accepted.Count > 0)
        {
            WriteAccepted(report, accepted, currentDirectory);
        }

        return report.ToString().TrimEnd();
    }

    private static void WriteTotals(
        StringBuilder report,
        AnalysisResult result,
        IReadOnlyList<IAnalysisRule> rules,
        IReadOnlyList<AcceptedFinding> accepted,
        IReadOnlySet<string> skippedRuleIds)
    {
        var errors = result.Issues.Count(issue => issue.Severity >= Severity.Error);
        var warnings = result.Issues.Count(issue => issue.Severity == Severity.Warning);
        var suggestions = result.Issues.Count - errors - warnings;
        var notPassed = result.Issues.Select(issue => issue.RuleId)
            .Concat(accepted.Select(finding => finding.RuleId))
            .Concat(skippedRuleIds)
            .ToHashSet();
        var passed = rules.Count(rule => !notPassed.Contains(rule.Id));

        report.AppendLine("| Errors | Warnings | Suggestions | Passed | Skipped |");
        report.AppendLine("| --- | --- | --- | --- | --- |");
        report.AppendLine($"| {errors} | {warnings} | {suggestions} | {passed} | {skippedRuleIds.Count} |");
        report.AppendLine();
    }

    private static void WriteNotes(StringBuilder report, IReadOnlyList<AcceptedFinding> accepted, int unmatchedBaselineEntries)
    {
        if (accepted.Count > 0)
        {
            var inline = accepted.Count(finding => finding.By == AcceptedBy.InlineComment);

            report.AppendLine($"> Not counted in the score: {inline} suppressed inline, {accepted.Count - inline} baselined.");
            report.AppendLine();
        }

        if (unmatchedBaselineEntries > 0)
        {
            var entries = unmatchedBaselineEntries == 1 ? "entry no longer matches" : "entries no longer match";

            report.AppendLine($"> {unmatchedBaselineEntries} baseline {entries} anything. "
                + "Run `aspireforge doctor --update-baseline` to remove fixed findings for good.");
            report.AppendLine();
        }
    }

    private static void WriteFindings(
        StringBuilder report,
        ProjectContext context,
        AnalysisResult result,
        IReadOnlyList<IAnalysisRule> rules,
        IReadOnlySet<string> fixableRuleIds,
        string currentDirectory)
    {
        var categories = RuleCatalog.Entries.ToDictionary(entry => entry.RuleId, entry => entry.Category);
        var titles = rules.ToDictionary(rule => rule.Id, rule => rule.Title);
        var issues = result.Issues
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.RuleId, StringComparer.Ordinal)
            .ToList();

        string RuleLink(AnalysisIssue issue) =>
            categories.TryGetValue(issue.RuleId, out var category) && titles.TryGetValue(issue.RuleId, out var title)
                ? $"[{issue.RuleId}]({RuleDocs.Url(category, issue.RuleId, title)})"
                : issue.RuleId;

        report.AppendLine($"### Findings ({issues.Count})");
        report.AppendLine();
        report.AppendLine("| Severity | Rule | Finding | Where |");
        report.AppendLine("| --- | --- | --- | --- |");

        foreach (var issue in issues)
        {
            var where = FindingFormatter.IsProjectWide(issue, context.ProjectFile) || issue.FilePath is not { Length: > 0 } file
                ? "whole project"
                : $"`{FindingFormatter.DisplayPath(file, currentDirectory)}{(issue.LineNumber is { } line ? $":{line}" : string.Empty)}`";

            report.AppendLine($"| {issue.Severity.ToString().ToLowerInvariant()} | {RuleLink(issue)} | {Escape(issue.Title)} | {where} |");
        }

        report.AppendLine();
        report.AppendLine("<details>");
        report.AppendLine("<summary>Why, and how to fix</summary>");
        report.AppendLine();

        foreach (var issue in issues)
        {
            report.AppendLine($"**{RuleLink(issue)} - {Escape(issue.Title)}**");
            report.AppendLine();
            report.AppendLine(Escape(issue.Description));
            report.AppendLine();

            if (RuleRemedies.CommandFor(issue.RuleId, fixableRuleIds) is { } command)
            {
                report.AppendLine($"Fix: `{command}`");
                report.AppendLine();
            }
        }

        report.AppendLine("</details>");
        report.AppendLine();
    }

    private static void WriteAccepted(StringBuilder report, IReadOnlyList<AcceptedFinding> accepted, string currentDirectory)
    {
        report.AppendLine($"### Accepted ({accepted.Count})");
        report.AppendLine();
        report.AppendLine("| Rule | Where | How |");
        report.AppendLine("| --- | --- | --- |");

        foreach (var finding in accepted)
        {
            var where = finding.File is null
                ? "whole project"
                : $"`{FindingFormatter.DisplayPath(finding.File, currentDirectory)}:{finding.Line}`";
            var how = finding.By == AcceptedBy.Baseline
                ? "baselined"
                : $"suppressed: {Escape(finding.Reason ?? "no reason given")}";

            report.AppendLine($"| {finding.RuleId} | {where} | {how} |");
        }

        report.AppendLine();
    }

    // Descriptions quote code, so `|` would end a table cell, `<` could start an HTML tag, and the `*` in
    // WithOrigins("*") could start emphasis.
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");
}
