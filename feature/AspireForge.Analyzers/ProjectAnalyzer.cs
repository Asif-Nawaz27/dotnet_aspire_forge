using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers;

public class ProjectAnalyzer(IEnumerable<IAnalysisRule> rules) : IProjectAnalyzer
{
    private readonly IReadOnlyList<IAnalysisRule> _rules = rules.ToList();

    public async Task<AnalysisResult> AnalyzeAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var results = await Task.WhenAll(
            _rules.Select(rule => rule.EvaluateAsync(context, cancellationToken)));

        var issues = results.Where(issue => issue is not null).Select(issue => issue!).ToList();

        return new AnalysisResult(context.ProjectName, issues);
    }
}
