namespace AspireForge.Core.Analysis;

public interface IAnalysisRule
{
    string Id { get; }

    string Title { get; }

    Severity DefaultSeverity { get; }

    Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default);
}
