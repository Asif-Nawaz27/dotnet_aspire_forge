using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

// EnsureCreated builds the schema straight from the current model and records no migration history,
// so later model changes can never be applied (and Migrate() will then fail on the existing tables).
public sealed partial class REL005EnsureCreatedUsed : IAnalysisRule
{
    private const int MaxLocationsInDescription = 5;

    public string Id => "REL005";

    public string Title => "EnsureCreated used instead of migrations";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match call in EnsureCreatedCall().Matches(code))
            {
                findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index)));
            }
        }

        findings.RemoveAll(f => context.Suppressions.IsSuppressed(Id, f.File, f.Line));

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings.OrderBy(f => f.File, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Line).ToList();

        var locations = string.Join(
            ", ",
            findings.Take(MaxLocationsInDescription).Select(f => $"{Path.GetFileName(f.File)}:{f.Line}"));
        var more = findings.Count > MaxLocationsInDescription
            ? $" (and {findings.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"A call to EnsureCreated() or EnsureCreatedAsync() was found ({locations}{more}). It bypasses migrations, so "
                + "schema changes made after the first run can never be applied - use Database.Migrate() or apply "
                + "migrations in your deployment pipeline.",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    [GeneratedRegex(@"\.EnsureCreated(Async)?\s*\(")]
    private static partial Regex EnsureCreatedCall();
}
