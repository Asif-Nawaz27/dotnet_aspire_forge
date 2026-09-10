namespace AspireForge.Core.Analysis;

public sealed record AnalysisResult(
    string ProjectName,
    IReadOnlyCollection<AnalysisIssue> Issues)
{
    public bool HasErrors =>
        Issues.Any(x => x.Severity == Severity.Error);
}
