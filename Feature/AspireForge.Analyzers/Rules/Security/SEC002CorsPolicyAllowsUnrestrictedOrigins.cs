using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// Flags CORS policies that accept requests from origins nobody chose:
//
//   error    AllowAnyOrigin(), or WithOrigins("*") - any website can call the API from a visitor's browser
//   error    SetIsOriginAllowed(...) with a predicate that always returns true (`_ => true`,
//            `o => { return true; }`, `delegate { return true; }`). Equivalent to AllowAnyOrigin - and the
//            usual way to get around ASP.NET refusing AllowAnyOrigin() together with AllowCredentials():
//            with credentials, any site can send requests carrying the visitor's cookies and read the
//            responses, which is why the message says so when AllowCredentials() is present.
//   warning  SetIsOriginAllowed(...) matching origins by substring - Contains("example.com") or
//            EndsWith("example.com") without a leading dot - which also accepts evil-example.com or
//            example.com.attacker.net
//
// Policies inside a development-only guard (`if (env.IsDevelopment())`, `#if DEBUG`) are allowed.
public sealed partial class SEC002CorsPolicyAllowsUnrestrictedOrigins : IAnalysisRule
{
    public string Id => "SEC002";

    public string Title => "CORS policy allows unrestricted origins";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line, Severity Severity, string Message)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);
            var credentials = AllowCredentials().IsMatch(code)
                ? " - and with AllowCredentials(), any site can make requests with the visitor's cookies and read the responses"
                : string.Empty;

            void Add(int index, Severity severity, string message)
            {
                if (!DevelopmentGuard.Covers(code, index))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, index), severity, message));
                }
            }

            foreach (Match match in AnyOrigin().Matches(code))
            {
                Add(match.Index, Severity.Error, $"{match.Value.TrimStart('.').Split('(')[0]}() allows every origin");
            }

            foreach (Match match in WildcardWithOrigins().Matches(code))
            {
                Add(match.Index, Severity.Error, "WithOrigins(\"*\") allows every origin");
            }

            foreach (Match match in OriginPredicate().Matches(code))
            {
                var predicate = SourceFileHeuristics.ReadCallArguments(code, match.Index + match.Length);

                if (AlwaysTrue().IsMatch(predicate))
                {
                    Add(match.Index, Severity.Error, $"SetIsOriginAllowed(...) accepts every origin{credentials}");
                }
                else if (SubstringMatch().IsMatch(predicate))
                {
                    Add(match.Index, Severity.Warning,
                        "SetIsOriginAllowed(...) matches origins by substring, so look-alike domains such as evil-example.com pass");
                }
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

        var listed = string.Join("; ", findings.Select(f => $"{f.Message} ({Path.GetFileName(f.File)}:{f.Line})"));

        return new AnalysisIssue(
            Id,
            Title,
            $"The CORS policy accepts origins nobody chose: {listed}. Restrict it to an explicit allow-list - "
                + "WithOrigins(\"https://app.example.com\") - read from configuration per environment.",
            findings[0].Severity,
            findings[0].File,
            findings[0].Line);
    }

    [GeneratedRegex(@"\.AllowAnyOrigin\s*\(")]
    private static partial Regex AnyOrigin();

    // WithOrigins("*") or WithOrigins("https://a.com", "*")
    [GeneratedRegex(@"\.WithOrigins\s*\([^)]*""\*""")]
    private static partial Regex WildcardWithOrigins();

    [GeneratedRegex(@"\.SetIsOriginAllowed\s*\(")]
    private static partial Regex OriginPredicate();

    [GeneratedRegex(@"\.AllowCredentials\s*\(")]
    private static partial Regex AllowCredentials();

    // _ => true, origin => true, (o) => true, o => { return true; }, delegate { return true; },
    // delegate (string o) { return true; }
    [GeneratedRegex(@"^\s*(?:(?:\(\s*\w*\s*\)|\w+)\s*=>\s*(?:true|\{\s*return\s+true\s*;\s*\})|delegate\s*(?:\([^)]*\))?\s*\{\s*return\s+true\s*;\s*\})\s*$")]
    private static partial Regex AlwaysTrue();

    // .Contains("...") anywhere, or .EndsWith("x...") where the suffix doesn't start with a dot.
    [GeneratedRegex(@"\.Contains\s*\(\s*""|\.EndsWith\s*\(\s*""(?!\.)")]
    private static partial Regex SubstringMatch();
}
