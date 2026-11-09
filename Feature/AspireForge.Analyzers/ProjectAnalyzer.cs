using AspireForge.Core.Analysis;
using AspireForge.Core.Configuration;

namespace AspireForge.Analyzers;

public class ProjectAnalyzer(
    IEnumerable<IAnalysisRule> rules,
    IFindingSuppressions? suppressions = null,
    ArchitectureStyle? architecture = null)
    : IProjectAnalyzer
{
    private readonly IReadOnlyList<IAnalysisRule> _rules = rules.ToList();
    private readonly IFindingSuppressions _suppressions = suppressions ?? NoFindingSuppressions.Instance;

    public async Task<AnalysisResult> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var context = BuildProjectContext(path) with { Suppressions = _suppressions, Architecture = architecture };
        var rules = LoadRules();
        var issues = await ExecuteRules(rules, context, cancellationToken);

        return ReturnAnalysisResult(context, issues);
    }

    private static ProjectContext BuildProjectContext(string path) => ProjectContextBuilder.Build(path);

    private IReadOnlyList<IAnalysisRule> LoadRules() => _rules;

    private static async Task<IReadOnlyCollection<AnalysisIssue>> ExecuteRules(
        IReadOnlyList<IAnalysisRule> rules,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(
            rules.Select(rule => rule.EvaluateAsync(context, cancellationToken)));

        // Occurrences with a line were already handled by the rule that found them; only it can see each one.
        return results
            .Where(issue => issue is not null)
            .Select(issue => issue!)
            .Where(issue => issue.LineNumber is not null || !context.Suppressions.IsSuppressed(issue))
            .ToList();
    }

    private static AnalysisResult ReturnAnalysisResult(
        ProjectContext context,
        IReadOnlyCollection<AnalysisIssue> issues) =>
        new(context.ProjectName, issues);
}
