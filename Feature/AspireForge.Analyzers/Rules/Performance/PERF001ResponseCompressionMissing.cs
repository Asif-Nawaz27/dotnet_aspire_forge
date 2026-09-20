using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Performance;

public sealed class PERF001ResponseCompressionMissing : IAnalysisRule
{
    public string Id => "PERF001";

    public string Title => "Response compression missing";

    public Severity DefaultSeverity => Severity.Suggestion;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddResponseCompression"))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No call to AddResponseCompression was found. Larger JSON responses are sent uncompressed, costing bandwidth and latency.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
