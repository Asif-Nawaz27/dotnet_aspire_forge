using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers;

public class ProjectAnalyzer(IEnumerable<IAnalysisRule> rules) : IProjectAnalyzer
{
    private readonly IReadOnlyList<IAnalysisRule> _rules = rules.ToList();

    public AnalysisResult Analyze(ProjectContext context)
    {
        var issues = _rules.SelectMany(rule => rule.Evaluate(context)).ToList();

        return new AnalysisResult(context.Project.Name, issues);
    }
}
