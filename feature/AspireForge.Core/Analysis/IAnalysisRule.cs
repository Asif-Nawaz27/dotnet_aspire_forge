namespace AspireForge.Core.Analysis;

public interface IAnalysisRule
{
    string Id { get; }

    string Description { get; }

    IEnumerable<AnalysisIssue> Evaluate(ProjectContext context);
}
