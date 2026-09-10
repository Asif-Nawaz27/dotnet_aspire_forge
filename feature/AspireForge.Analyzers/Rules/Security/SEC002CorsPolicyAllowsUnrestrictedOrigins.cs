using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

public sealed class SEC002CorsPolicyAllowsUnrestrictedOrigins : IAnalysisRule
{
    public string Id => "SEC002";

    public string Title => "CORS policy allows unrestricted origins";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AllowAnyOrigin"))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "A CORS policy calls AllowAnyOrigin, which permits requests from any origin. Restrict this to a known allow-list.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
