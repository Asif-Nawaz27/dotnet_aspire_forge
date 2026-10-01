using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

// Without EnableRetryOnFailure, a transient database error (failover, brief network drop, throttling)
// surfaces as a failed request instead of being retried. Checked across all source files because the
// UseNpgsql/UseSqlServer call and its options lambda may live in a referenced Infrastructure project.
public sealed partial class REL006DatabaseRetryPolicyMissing : IAnalysisRule
{
    public string Id => "REL006";

    public string Title => "Database retry policy missing";

    public Severity DefaultSeverity => Severity.Suggestion;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.MatchesAsync(context, cancellationToken, ProviderCall()))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "EnableRetryOnFailure"))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "UseNpgsql or UseSqlServer was found, but no call to EnableRetryOnFailure. Transient database errors will fail requests instead of being retried.",
            DefaultSeverity,
            context.ProjectFile);
    }

    [GeneratedRegex(@"\.(UseNpgsql|UseSqlServer)\s*\(")]
    private static partial Regex ProviderCall();
}
