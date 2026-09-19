namespace AspireForge.Core.Analysis;

public sealed record AnalysisIssue(
    string RuleId,
    string Title,
    string Description,
    Severity Severity,
    string? FilePath = null,
    int? LineNumber = null);
