using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

public sealed class SEC003HttpsRedirectionMissing : IAnalysisRule
{
    public string Id => "SEC003";

    public string Title => "HTTPS redirection missing";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "UseHttpsRedirection"))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No call to UseHttpsRedirection was found. HTTP requests will not be automatically upgraded to HTTPS.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
