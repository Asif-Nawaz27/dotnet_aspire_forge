using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// Middleware runs in the order it's added, and several pairs only work one way round - the wrong order
// compiles, starts, and passes a casual smoke test, then breaks authentication, CORS, or error handling
// under real traffic. Each pair below is checked only when both calls appear in the same file (the
// pipeline is almost always built in one place), by the position of each call's first occurrence.
//
// Ordering follows the ASP.NET Core middleware documentation:
//   https://learn.microsoft.com/aspnet/core/fundamentals/middleware/#middleware-order
// UseRouting pairs only apply when UseRouting() is called explicitly - without it, WebApplication adds
// routing at the start of the pipeline itself.
public sealed class SEC009MiddlewareOrder : IAnalysisRule
{
    private const int MaxViolationsInDescription = 3;

    private static readonly OrderingRequirement[] Requirements =
    [
        new("UseAuthentication", "UseAuthorization", Severity.Error,
            "authorization runs before the caller is identified, so requests to protected endpoints are rejected even with valid credentials"),
        new("UseRouting", "UseAuthorization", Severity.Error,
            "authorization runs before an endpoint is selected, so it can't see the endpoint's [Authorize] or RequireAuthorization() requirements"),
        new("UseCors", "UseAuthorization", Severity.Warning,
            "CORS preflight (OPTIONS) requests carry no credentials, so authorization rejects them before CORS can answer and browsers block the call"),
        new("UseRouting", "UseCors", Severity.Warning,
            "CORS runs before an endpoint is selected, so endpoint-specific policies ([EnableCors], RequireCors) are ignored"),
        new("UseRouting", "UseRateLimiter", Severity.Warning,
            "rate limiting runs before an endpoint is selected, so endpoint-specific policies ([EnableRateLimiting], RequireRateLimiting) are ignored"),
        .. ExceptionHandlerFirst("UseExceptionHandler"),
        .. ExceptionHandlerFirst("UseDeveloperExceptionPage"),
    ];

    public string Id => "SEC009";

    public string Title => "Middleware in the wrong order";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<Violation>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (var requirement in Requirements)
            {
                var first = FirstCall(code, requirement.First);
                var then = FirstCall(code, requirement.Then);

                if (first is not null && then is not null && then.Index < first.Index)
                {
                    violations.Add(new Violation(file, SourceFileHeuristics.LineOf(code, then.Index), requirement));
                }
            }
        }

        if (violations.Count == 0)
        {
            return null;
        }

        violations = violations
            .OrderByDescending(violation => violation.Requirement.Severity)
            .ThenBy(violation => violation.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(violation => violation.Line)
            .ToList();

        var listed = string.Join("; ", violations.Take(MaxViolationsInDescription).Select(violation => violation.Describe()));
        var more = violations.Count > MaxViolationsInDescription
            ? $" (and {violations.Count - MaxViolationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"{listed}{more}.",
            violations[0].Requirement.Severity,
            violations[0].File,
            violations[0].Line);
    }

    // Exceptions thrown by any middleware added before the handler bypass it entirely.
    private static IEnumerable<OrderingRequirement> ExceptionHandlerFirst(string handler) =>
        new[] { "UseRouting", "UseCors", "UseAuthentication", "UseAuthorization", "UseRateLimiter", "UseResponseCompression" }
            .Select(middleware => new OrderingRequirement(handler, middleware, Severity.Warning,
                $"exceptions thrown by {middleware} bypass the error handler and reach the client unformatted"));

    private static Match? FirstCall(string code, string method)
    {
        var match = Regex.Match(code, $@"\.{method}\s*[<(]");
        return match.Success ? match : null;
    }

    private sealed record OrderingRequirement(string First, string Then, Severity Severity, string Consequence);

    private sealed record Violation(string File, int Line, OrderingRequirement Requirement)
    {
        public string Describe() =>
            $"{Requirement.Then}() is called before {Requirement.First}() ({Path.GetFileName(File)}:{Line}) - {Requirement.Consequence}";
    }
}
