using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

// Two separate failure modes, one rule: `new HttpClient(` skips IHttpClientFactory entirely (socket
// exhaustion and stale DNS), and AddHttpClient without any resilience handler leaves every outbound
// call without retries, timeouts, or a circuit breaker. A ConfigureHttpClientDefaults(... AddStandard
// ResilienceHandler()) in ServiceDefaults counts - it applies to every client - so the handler tokens
// are searched across all source files, not just next to the AddHttpClient call.
public sealed partial class REL003OutboundHttpWithoutResilience : IAnalysisRule
{
    private static readonly string[] ResilienceTokens =
    [
        "AddStandardResilienceHandler",
        "AddResilienceHandler",
        "AddStandardHedgingHandler",
        "AddPolicyHandler",
        "AddTransientHttpErrorPolicy",
    ];

    public string Id => "REL003";

    public string Title => "Outbound HTTP without resilience";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (await SourceFileHeuristics.MatchesAsync(context, cancellationToken, NewHttpClient()))
        {
            return new AnalysisIssue(
                Id,
                Title,
                "A direct 'new HttpClient(' was found. Creating clients by hand bypasses IHttpClientFactory, which risks socket exhaustion and stale DNS - register a client with AddHttpClient and add AddStandardResilienceHandler instead.",
                DefaultSeverity,
                context.ProjectFile);
        }

        if (!await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddHttpClient", "ConfigureHttpClientDefaults"))
        {
            return null;
        }

        // A resilience token in source is proof enough; the package is often referenced by a
        // referenced project (ServiceDefaults) rather than directly, so it isn't checked.
        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, ResilienceTokens))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "AddHttpClient was found, but no resilience handler (AddStandardResilienceHandler, AddResilienceHandler, or a Polly policy). Outbound calls have no retries, timeouts, or circuit breaker, so one slow dependency can stall this service.",
            DefaultSeverity,
            context.ProjectFile);
    }

    [GeneratedRegex(@"\bnew\s+HttpClient\s*\(")]
    private static partial Regex NewHttpClient();
}
