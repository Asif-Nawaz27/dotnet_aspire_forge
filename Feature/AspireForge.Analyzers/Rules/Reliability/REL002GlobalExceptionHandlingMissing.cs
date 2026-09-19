using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

public sealed class REL002GlobalExceptionHandlingMissing : IAnalysisRule
{
    public string Id => "REL002";

    public string Title => "Global exception handling missing";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(
                context, cancellationToken, "UseExceptionHandler", "AddExceptionHandler"))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No global exception handler was found. Unhandled exceptions will produce default, unformatted error responses.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
