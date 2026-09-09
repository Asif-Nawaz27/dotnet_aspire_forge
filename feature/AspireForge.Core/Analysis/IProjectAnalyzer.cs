namespace AspireForge.Core.Analysis;

public interface IProjectAnalyzer
{
    AnalysisResult Analyze(ProjectContext context);
}
