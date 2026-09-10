using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

public sealed class SEC001AuthenticationNotConfigured : IAnalysisRule
{
    public string Id => "SEC001";

    public string Title => "Authentication not configured";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddAuthentication"))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No call to AddAuthentication was found. If this API requires callers to be identified, register an authentication scheme.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
