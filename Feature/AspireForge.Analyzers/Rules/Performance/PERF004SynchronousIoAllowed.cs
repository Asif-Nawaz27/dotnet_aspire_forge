using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Performance;

// ASP.NET Core disallows synchronous reads and writes of the request and response bodies by default:
// each blocking call parks a thread-pool thread while waiting on the network, and under load the pool
// runs dry - requests queue, latency climbs, and the server can stop responding entirely (thread-pool
// starvation). AllowSynchronousIO = true turns that protection off.
//
//   warning     server-wide - KestrelServerOptions / IISServerOptions / TestServer options:
//               options.AllowSynchronousIO = true
//   suggestion  per request - IHttpBodyControlFeature.AllowSynchronousIO = true. Microsoft's documented
//               workaround for a library that only does synchronous I/O; it limits the damage to the
//               endpoints that set it, but those endpoints still block threads.
// Assignments inside a development-only guard (`if (env.IsDevelopment())`, `#if DEBUG`) are allowed.
public sealed partial class PERF004SynchronousIoAllowed : IAnalysisRule
{
    private const int MaxLocationsInDescription = 3;

    public string Id => "PERF004";

    public string Title => "Synchronous I/O allowed";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line, Severity Severity, string Scope)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match match in AllowSynchronousIoTrue().Matches(code))
            {
                if (DevelopmentGuard.Covers(code, match.Index))
                {
                    continue;
                }

                var perRequest = IsPerRequest(code, match.Index);

                findings.Add((
                    file,
                    SourceFileHeuristics.LineOf(code, match.Index),
                    perRequest ? Severity.Suggestion : Severity.Warning,
                    perRequest ? "for this request" : "server-wide"));
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Line)
            .ToList();

        var locations = string.Join(
            ", ",
            findings.Take(MaxLocationsInDescription).Select(f => $"{f.Scope} at {Path.GetFileName(f.File)}:{f.Line}"));
        var more = findings.Count > MaxLocationsInDescription
            ? $" (and {findings.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"AllowSynchronousIO is enabled ({locations}{more}). Synchronous body reads and writes block a thread-pool "
                + "thread per request while waiting on the network, and under load the server runs out of threads. Use the "
                + "async APIs (ReadAsync, WriteAsync, ReadFromJsonAsync), or buffer the body first "
                + "(HttpRequest.EnableBuffering) for a library that only reads synchronously.",
            findings[0].Severity,
            findings[0].File,
            findings[0].Line);
    }

    // Per request when the statement itself goes through the feature collection, or when the variable it
    // assigns through was obtained from it - Microsoft's documented pattern:
    //   var feature = HttpContext.Features.Get<IHttpBodyControlFeature>();
    //   if (feature != null) { feature.AllowSynchronousIO = true; }
    private static bool IsPerRequest(string code, int index)
    {
        var statementStart = code.LastIndexOfAny([';', '{', '}'], Math.Max(0, index - 1)) + 1;
        var statement = code[statementStart..index];

        if (PerRequestFeature().IsMatch(statement))
        {
            return true;
        }

        var receiver = Receiver().Match(statement);
        if (!receiver.Success)
        {
            return false;
        }

        var name = Regex.Escape(receiver.Groups["name"].Value);
        return Regex.IsMatch(code, $@"\b{name}\s*=(?!=)[^;]*(?:IHttpBodyControlFeature|\.Features\b)")
            || Regex.IsMatch(code, $@"\bIHttpBodyControlFeature\??\s+{name}\b");
    }

    [GeneratedRegex(@"\bAllowSynchronousIO\s*=(?!=)\s*true\b")]
    private static partial Regex AllowSynchronousIoTrue();

    // The variable in front of `.AllowSynchronousIO` (also `feature!.` / `feature?.`).
    [GeneratedRegex(@"\b(?<name>\w+)\s*[!?]?\s*\.\s*$")]
    private static partial Regex Receiver();

    // The statement goes through the request's feature collection rather than server options.
    [GeneratedRegex(@"\bIHttpBodyControlFeature\b|\.Features\b")]
    private static partial Regex PerRequestFeature();
}
