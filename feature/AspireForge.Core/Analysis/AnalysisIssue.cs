namespace AspireForge.Core.Analysis;

public class AnalysisIssue
{
    public required string RuleId { get; init; }

    public required string Message { get; init; }

    public Severity Severity { get; init; }

    public string? FilePath { get; init; }

    public int? Line { get; init; }
}
