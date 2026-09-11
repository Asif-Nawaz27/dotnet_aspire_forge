using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers;

public class ProjectAnalyzer(IEnumerable<IAnalysisRule> rules) : IProjectAnalyzer
{
    private readonly IReadOnlyList<IAnalysisRule> _rules = rules.ToList();

    public async Task<AnalysisResult> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var context = BuildProjectContext(path);
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

        return results.Where(issue => issue is not null).Select(issue => issue!).ToList();
    }

    private static AnalysisResult ReturnAnalysisResult(
        ProjectContext context,
        IReadOnlyCollection<AnalysisIssue> issues) =>
        new(context.ProjectName, issues);
}
