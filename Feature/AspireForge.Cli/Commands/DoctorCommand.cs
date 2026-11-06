using System.Text.Json;
using AspireForge.Analyzers;
using AspireForge.Analyzers.Configuration;
using AspireForge.Analyzers.Suppressions;
using AspireForge.Cli.Output;
using AspireForge.Core.Analysis;
using AspireForge.Generators;

namespace AspireForge.Cli.Commands;

public class DoctorCommand(ConsoleRenderer renderer)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<int> RunAsync(
        string path,
        Severity failOn = Severity.Error,
        OutputFormat format = OutputFormat.Text,
        bool scanVulnerablePackages = false,
        bool summaryOnly = false,
        bool updateBaseline = false,
        bool ignoreBaseline = false,
        CancellationToken cancellationToken = default)
    {
        ProjectContext context;

        try
        {
            context = ProjectContextBuilder.Build(path);
        }
        catch (FileNotFoundException)
        {
            renderer.WriteLine($"No project file found at '{path}'.");
            return ExitCodes.InvalidConfiguration;
        }

        var config = ConfigLoader.Load(context.RootDirectory);
        var rules = AnalysisRuleSet.ApplyConfig(AnalysisRuleSet.CreateDefault(scanVulnerablePackages), config);

        if (updateBaseline)
        {
            await WriteBaselineAsync(path, context, rules, cancellationToken);
            return ExitCodes.Success;
        }

        BaselineFile? baseline;

        try
        {
            baseline = ignoreBaseline ? null : BaselineFile.Load(context.RootDirectory);
        }
        catch (InvalidDataException exception)
        {
            renderer.WriteLine(exception.Message);
            return ExitCodes.InvalidConfiguration;
        }

        var suppressions = FindingSuppressions.Create(context.RootDirectory, baseline);
        var analyzer = new ProjectAnalyzer(rules, suppressions);
        var rawResult = await analyzer.AnalyzeAsync(path, cancellationToken);
        var result = AnalysisRuleSet.ApplySeverityOverrides(rawResult, config);

        var shouldFail = result.Issues.Any(issue => issue.Severity >= failOn);

        switch (format)
        {
            case OutputFormat.Json:
                WriteJsonReport(result, suppressions.Accepted);
                return shouldFail ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;

            case OutputFormat.Sarif:
                WriteSarifReport(result, rules);
                return shouldFail ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;
        }

        WriteTextReport(context, result, rules, suppressions, showDetails: !summaryOnly);

        return shouldFail ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;
    }

    // Runs the rules once and accepts whatever they'd report. Entries for rules that didn't run are
    // kept: `--update-baseline` without --vulnerable mustn't forget the accepted DEP002 findings.
    private async Task WriteBaselineAsync(
        string path, ProjectContext context, IReadOnlyList<IAnalysisRule> rules, CancellationToken cancellationToken)
    {
        var recorder = FindingSuppressions.Recording(context.RootDirectory);
        await new ProjectAnalyzer(rules, recorder).AnalyzeAsync(path, cancellationToken);

        var rulesThatRan = rules.Select(rule => rule.Id).ToHashSet();
        var recorded = recorder.RecordedEntries;
        var kept = ReadExistingBaseline(context.RootDirectory).Where(entry => !rulesThatRan.Contains(entry.RuleId));

        new BaselineFile(BaselineFile.CurrentVersion, [.. kept, .. recorded]).Save(context.RootDirectory);

        var total = recorded.Sum(entry => entry.Count);
        var location = FindingFormatter.DisplayPath(BaselineFile.PathFor(context.RootDirectory), Directory.GetCurrentDirectory());

        renderer.WriteLine($"Baseline written to {location}: {total} finding{(total == 1 ? string.Empty : "s")} accepted.");

        foreach (var rule in recorded.GroupBy(entry => entry.RuleId).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            renderer.WriteLine($"  {rule.Key.PadRight(8)} {rule.Sum(entry => entry.Count)}");
        }

        renderer.WriteLine();
        renderer.WriteLine("Commit this file. doctor now reports only findings that aren't in it.");
    }

    // A damaged baseline is being replaced anyway, so it shouldn't stop that from happening.
    private static IReadOnlyList<BaselineEntry> ReadExistingBaseline(string projectDirectory)
    {
        try
        {
            return BaselineFile.Load(projectDirectory)?.Findings ?? [];
        }
        catch (InvalidDataException)
        {
            return [];
        }
    }

    // The checklist (one line per enabled rule, grouped by category), then - unless --summary - a
    // block per finding with where it is, why it matters, how to fix it, and a link to the rule's docs.
    private void WriteTextReport(
        ProjectContext context,
        AnalysisResult result,
        IReadOnlyList<IAnalysisRule> rules,
        FindingSuppressions suppressions,
        bool showDetails)
    {
        var issuesByRuleId = result.Issues.ToDictionary(issue => issue.RuleId);
        var accepted = suppressions.Accepted;
        var acceptedRuleIds = accepted.Select(finding => finding.RuleId).ToHashSet();
        var enabledRuleIds = rules.Select(rule => rule.Id).ToHashSet();
        var activeChecks = RuleCatalog.Entries.Where(check => enabledRuleIds.Contains(check.RuleId)).ToList();
        var currentDirectory = Directory.GetCurrentDirectory();

        renderer.WriteLine("AspireForge Doctor");
        renderer.WriteLine();
        renderer.WriteLine($"Project: {context.ProjectName}");
        renderer.WriteLine($"Framework: {context.TargetFramework ?? "unknown"}");
        renderer.WriteLine($"Path: {FindingFormatter.DisplayPath(context.ProjectFile, currentDirectory)}");

        var errorCount = 0;
        var warningCount = 0;
        var suggestionCount = 0;
        var passedCount = 0;

        foreach (var category in activeChecks.Select(check => check.Category).Distinct())
        {
            renderer.WriteSectionHeader(category);

            foreach (var check in activeChecks.Where(check => check.Category == category))
            {
                if (!issuesByRuleId.TryGetValue(check.RuleId, out var issue))
                {
                    // Not a pass: the problem is still there, someone has only accepted it.
                    if (acceptedRuleIds.Contains(check.RuleId))
                    {
                        renderer.WriteCheck("-", $"{check.ShortName}: accepted", check.RuleId);
                        continue;
                    }

                    renderer.WriteCheck("✓", check.PassedMessage);
                    passedCount++;
                    continue;
                }

                renderer.WriteCheck(FindingFormatter.Symbol(issue.Severity), issue.Title, check.RuleId);

                switch (issue.Severity)
                {
                    case Severity.Error or Severity.Critical:
                        errorCount++;
                        break;
                    case Severity.Warning:
                        warningCount++;
                        break;
                    default:
                        suggestionCount++;
                        break;
                }
            }
        }

        var fixableRuleIds = RuleFixSet.CreateDefault().Select(fix => fix.RuleId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (showDetails && result.Issues.Count > 0)
        {
            WriteFindings(context, result, rules, activeChecks, fixableRuleIds, currentDirectory);
        }

        if (showDetails && accepted.Count > 0)
        {
            WriteAccepted(accepted, currentDirectory);
        }

        renderer.WriteLine();
        renderer.WriteSeparator();
        renderer.WriteLine($"Errors:      {errorCount}");
        renderer.WriteLine($"Warnings:    {warningCount}");
        renderer.WriteLine($"Suggestions: {suggestionCount}");
        renderer.WriteLine($"Passed:      {passedCount}");

        var inlineCount = accepted.Count(finding => finding.By == AcceptedBy.InlineComment);
        var baselinedCount = accepted.Count - inlineCount;

        if (accepted.Count > 0)
        {
            renderer.WriteLine($"Suppressed:  {inlineCount}");
            renderer.WriteLine($"Baselined:   {baselinedCount}");
        }

        renderer.WriteSeparator();
        renderer.WriteLine();

        WriteScore(result, activeChecks);

        // After the score rather than in it: the score is only as good as what was accepted, so it has
        // to say so right there.
        if (accepted.Count > 0)
        {
            renderer.WriteLine();
            renderer.WriteLine($"Not counted in the score: {inlineCount} suppressed inline, {baselinedCount} baselined.");

            if (baselinedCount > 0)
            {
                renderer.WriteLine("Run with --no-baseline to see the score with baselined findings included.");
            }
        }

        var unmatched = suppressions.UnmatchedBaselineCount(rules.Select(rule => rule.Id).ToHashSet());

        if (unmatched > 0)
        {
            renderer.WriteLine();
            renderer.WriteLine($"{unmatched} baseline {(unmatched == 1 ? "entry no longer matches" : "entries no longer match")} anything. "
                + "Run doctor --update-baseline to remove fixed findings for good.");
        }

        if (result.Issues.Count == 0)
        {
            return;
        }

        // The quickest wins: findings a single command resolves, one line per command.
        var remedies = result.Issues
            .OrderByDescending(issue => issue.Severity)
            .Select(issue => (issue.RuleId, Command: RuleRemedies.CommandFor(issue.RuleId, fixableRuleIds)))
            .Where(remedy => remedy.Command is not null)
            .GroupBy(remedy => remedy.Command!)
            .ToList();

        renderer.WriteLine();
        renderer.WriteLine("Next steps");

        foreach (var remedy in remedies)
        {
            renderer.WriteLine($"  {remedy.Key.PadRight(32)} resolves {string.Join(", ", remedy.Select(r => r.RuleId))}");
        }

        var manual = result.Issues.Count - remedies.Sum(remedy => remedy.Count());

        if (manual > 0)
        {
            renderer.WriteLine(showDetails
                ? $"  {manual} finding{(manual == 1 ? " needs" : "s need")} a code change - see Why and Docs above."
                : $"  {manual} finding{(manual == 1 ? " needs" : "s need")} a code change - run without --summary to see where and how.");
        }
    }

    // The overall score, why it isn't higher, and the same score per category - so it's clear where
    // the points went. See ReadinessScore for the formula.
    private void WriteScore(AnalysisResult result, IReadOnlyList<RuleCatalogEntry> activeChecks)
    {
        var score = ReadinessScore.Calculate(result.Issues);
        var reason = score.CapReason is not null ? $" - {score.CapReason}" : string.Empty;

        renderer.WriteLine($"Production Readiness: {score.OutOfTen}/10 ({score.Points}/100){reason}");

        if (result.Issues.Count == 0)
        {
            return;
        }

        var categoryOf = activeChecks.ToDictionary(check => check.RuleId, check => check.Category);

        renderer.WriteLine();

        foreach (var category in activeChecks.Select(check => check.Category).Distinct())
        {
            var issues = result.Issues
                .Where(issue => categoryOf.TryGetValue(issue.RuleId, out var c) && c == category)
                .ToList();
            var categoryScore = ReadinessScore.Calculate(issues);
            var detail = issues.Count == 0
                ? string.Empty
                : $"   -{categoryScore.Penalty} ({DescribeCounts(issues)})";

            renderer.WriteLine($"  {category.PadRight(15)}{categoryScore.Points,3}/100{detail}");
        }

        renderer.WriteLine();
        renderer.WriteLine("  Score: 100 minus 30 per critical, 15 per error, 5 per warning, 2 per suggestion, 1 per info;");
        renderer.WriteLine($"  any error caps it at {ReadinessScore.ErrorCap}, any critical at {ReadinessScore.CriticalCap}.");
    }

    // "1 error, 2 warnings, 3 info"
    private static string DescribeCounts(IEnumerable<AnalysisIssue> issues) =>
        string.Join(", ", issues
            .GroupBy(issue => issue.Severity)
            .OrderByDescending(group => group.Key)
            .Select(group =>
            {
                var noun = group.Key.ToString().ToLowerInvariant();
                return $"{group.Count()} {noun}{(group.Count() == 1 || group.Key == Severity.Info ? string.Empty : "s")}";
            }));

    // Most severe first, then by rule ID, so the findings to act on first are at the top.
    private void WriteFindings(
        ProjectContext context,
        AnalysisResult result,
        IReadOnlyList<IAnalysisRule> rules,
        IReadOnlyList<RuleCatalogEntry> activeChecks,
        IReadOnlySet<string> fixableRuleIds,
        string currentDirectory)
    {
        var categories = activeChecks.ToDictionary(check => check.RuleId, check => check.Category);
        var titles = rules.ToDictionary(rule => rule.Id, rule => rule.Title);

        renderer.WriteLine();
        renderer.WriteBanner($"Findings ({result.Issues.Count})");

        foreach (var issue in result.Issues
                     .OrderByDescending(issue => issue.Severity)
                     .ThenBy(issue => issue.RuleId, StringComparer.Ordinal))
        {
            var docsUrl = categories.TryGetValue(issue.RuleId, out var category) && titles.TryGetValue(issue.RuleId, out var title)
                ? RuleDocs.Url(category, issue.RuleId, title)
                : null;

            renderer.WriteLine();

            foreach (var line in FindingFormatter.Format(
                         issue,
                         docsUrl,
                         RuleRemedies.CommandFor(issue.RuleId, fixableRuleIds),
                         context.ProjectFile,
                         currentDirectory,
                         renderer.Width))
            {
                renderer.WriteLine(line);
            }
        }
    }

    // What was left out of the report and why, so accepting a finding never makes it disappear.
    private void WriteAccepted(IReadOnlyList<AcceptedFinding> accepted, string currentDirectory)
    {
        renderer.WriteLine();
        renderer.WriteBanner($"Accepted ({accepted.Count})");
        renderer.WriteLine();

        foreach (var finding in accepted)
        {
            var location = finding.File is null
                ? "whole project"
                : $"{FindingFormatter.DisplayPath(finding.File, currentDirectory)}:{finding.Line}";
            var how = finding.By == AcceptedBy.Baseline
                ? "baselined"
                : $"suppressed: {finding.Reason ?? "no reason given"}";

            renderer.WriteLine($"  {finding.RuleId.PadRight(8)} {location} - {how}");
        }
    }

    private void WriteJsonReport(AnalysisResult result, IReadOnlyList<AcceptedFinding> accepted)
    {
        var report = new DoctorJsonReport(
            result.ProjectName,
            result.Issues
                .Select(issue => new DoctorJsonIssue(
                    issue.RuleId, issue.Severity.ToString().ToLowerInvariant(), issue.Title))
                .ToList(),
            accepted.Count(finding => finding.By == AcceptedBy.InlineComment),
            accepted.Count(finding => finding.By == AcceptedBy.Baseline));

        renderer.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
    }

    private void WriteSarifReport(AnalysisResult result, IReadOnlyList<IAnalysisRule> rules)
    {
        var log = SarifReportBuilder.Build(result, rules);
        renderer.WriteLine(JsonSerializer.Serialize(log, JsonOptions));
    }
}
