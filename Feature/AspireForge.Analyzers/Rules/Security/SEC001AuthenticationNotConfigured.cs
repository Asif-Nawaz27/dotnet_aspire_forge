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

        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddAuthentication"))
        {
            return new AnalysisIssue(
                Id,
                Title,
                "No call to AddAuthentication was found. If this API requires callers to be identified, register an authentication scheme.",
                DefaultSeverity,
                context.ProjectFile);
        }

        // A registered scheme does nothing unless the middleware is actually added to the pipeline -
        // a real, easy mistake (register the service, forget app.UseAuthentication()), not just a
        // hypothetical one this rule should catch.
        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "UseAuthentication"))
        {
            return new AnalysisIssue(
                Id,
                Title,
                "AddAuthentication was found, but app.UseAuthentication() was not. The scheme is registered but the middleware never runs, so requests are never actually authenticated.",
                DefaultSeverity,
                context.ProjectFile);
        }

        return null;
    }
}
