using System.Text.Json;
using AspireForge.Analyzers;
using AspireForge.Analyzers.Configuration;
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
        var analyzer = new ProjectAnalyzer(rules);
        var rawResult = await analyzer.AnalyzeAsync(path, cancellationToken);
        var result = AnalysisRuleSet.ApplySeverityOverrides(rawResult, config);

        var shouldFail = result.Issues.Any(issue => issue.Severity >= failOn);

        switch (format)
        {
            case OutputFormat.Json:
                WriteJsonReport(result);
                return shouldFail ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;

            case OutputFormat.Sarif:
                WriteSarifReport(result, rules);
                return shouldFail ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;
        }

        WriteTextReport(context, result, rules, showDetails: !summaryOnly);

        return shouldFail ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;
    }

    // The checklist (one line per enabled rule, grouped by category), then - unless --summary - a
    // block per finding with where it is, why it matters, how to fix it, and a link to the rule's docs.
    private void WriteTextReport(
        ProjectContext context, AnalysisResult result, IReadOnlyList<IAnalysisRule> rules, bool showDetails)
    {
        var issuesByRuleId = result.Issues.ToDictionary(issue => issue.RuleId);
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

        renderer.WriteLine();
        renderer.WriteSeparator();
        renderer.WriteLine($"Errors:      {errorCount}");
        renderer.WriteLine($"Warnings:    {warningCount}");
        renderer.WriteLine($"Suggestions: {suggestionCount}");
        renderer.WriteLine($"Passed:      {passedCount}");
        renderer.WriteSeparator();
        renderer.WriteLine();

        var score = activeChecks.Count == 0 ? 10 : (int)Math.Round(10.0 * passedCount / activeChecks.Count);
        renderer.WriteLine($"Production Readiness: {score}/10");

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

    private void WriteJsonReport(AnalysisResult result)
    {
        var report = new DoctorJsonReport(
            result.ProjectName,
            result.Issues
                .Select(issue => new DoctorJsonIssue(
                    issue.RuleId, issue.Severity.ToString().ToLowerInvariant(), issue.Title))
                .ToList());

        renderer.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
    }

    private void WriteSarifReport(AnalysisResult result, IReadOnlyList<IAnalysisRule> rules)
    {
        var log = SarifReportBuilder.Build(result, rules);
        renderer.WriteLine(JsonSerializer.Serialize(log, JsonOptions));
    }
}
