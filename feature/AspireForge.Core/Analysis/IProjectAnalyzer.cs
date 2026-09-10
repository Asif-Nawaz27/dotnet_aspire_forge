namespace AspireForge.Core.Analysis;

public interface IProjectAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken = default);
}
