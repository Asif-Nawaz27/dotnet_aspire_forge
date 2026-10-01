using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

// EnsureCreated builds the schema straight from the current model and records no migration history,
// so later model changes can never be applied (and Migrate() will then fail on the existing tables).
public sealed partial class REL005EnsureCreatedUsed : IAnalysisRule
{
    public string Id => "REL005";

    public string Title => "EnsureCreated used instead of migrations";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.MatchesAsync(context, cancellationToken, EnsureCreatedCall()))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "A call to EnsureCreated() or EnsureCreatedAsync() was found. It bypasses migrations, so schema changes made after the first run can never be applied - use Database.Migrate() or apply migrations in your deployment pipeline.",
            DefaultSeverity,
            context.ProjectFile);
    }

    [GeneratedRegex(@"\.EnsureCreated(Async)?\s*\(")]
    private static partial Regex EnsureCreatedCall();
}
