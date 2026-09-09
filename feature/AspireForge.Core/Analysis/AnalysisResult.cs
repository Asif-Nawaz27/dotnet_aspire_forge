namespace AspireForge.Core.Analysis;

public class AnalysisResult
{
    public required string ProjectName { get; init; }

    public IReadOnlyList<AnalysisIssue> Issues { get; init; } = [];
}
