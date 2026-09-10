namespace AspireForge.Core.Analysis;

public interface IProjectAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default);
}
