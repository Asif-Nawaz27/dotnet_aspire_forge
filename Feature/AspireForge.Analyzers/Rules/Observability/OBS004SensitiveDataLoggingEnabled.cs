using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

// EF Core's EnableSensitiveDataLogging() writes query parameter values - emails, names, tokens,
// anything a query filters or saves - into logs and exception messages. Useful locally; in production
// it copies personal data into every log sink, which is both a privacy breach and often a compliance
// one (GDPR, PCI).
//
// Each call is flagged unless something limits it to development. Recognised guards:
//   - the bool overload with an environment check: EnableSensitiveDataLogging(env.IsDevelopment())
//   - an explicit EnableSensitiveDataLogging(false)
//   - an enclosing `if (...IsDevelopment()...)` - braced or braceless - that isn't negated
//   - an enclosing `#if DEBUG`
// This is a text heuristic, not data-flow analysis: a guard through a variable (`var dev =
// env.IsDevelopment(); if (dev) ...`) isn't recognised - pass the check inline instead.
public sealed partial class OBS004SensitiveDataLoggingEnabled : IAnalysisRule
{
    private const int MaxLocationsInDescription = 3;

    public string Id => "OBS004";

    public string Title => "Sensitive data logging enabled";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match call in SensitiveDataLoggingCall().Matches(code))
            {
                var argument = ReadArgument(code, call.Index + call.Length);

                if (!IsLimitedToDevelopment(code, call.Index, argument))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index)));
                }
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        var locations = string.Join(
            ", ",
            findings.Take(MaxLocationsInDescription).Select(finding => $"{Path.GetFileName(finding.File)}:{finding.Line}"));
        var more = findings.Count > MaxLocationsInDescription
            ? $" (and {findings.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"EnableSensitiveDataLogging() is called without a development-only guard ({locations}{more}). It writes "
                + "query parameter values - including personal data - into logs and exception messages. Pass "
                + "builder.Environment.IsDevelopment() as its argument, or wrap the call in an IsDevelopment() check.",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    private static bool IsLimitedToDevelopment(string code, int callIndex, string argument)
    {
        var trimmed = argument.Trim();

        if (trimmed == "false" || IsDevelopmentCheck(trimmed))
        {
            return true;
        }

        return IsInsideDebugDirective(code, callIndex)
            || IsInBracelessDevelopmentIf(code, callIndex)
            || IsInsideDevelopmentBlock(code, callIndex);
    }

    // `if (env.IsDevelopment()) options.EnableSensitiveDataLogging();` - the statement header is
    // everything since the previous statement or block boundary.
    private static bool IsInBracelessDevelopmentIf(string code, int callIndex)
    {
        var start = code.LastIndexOfAny([';', '{', '}'], Math.Max(0, callIndex - 1)) + 1;
        return IsDevelopmentIfHeader(code[start..callIndex]);
    }

    // Walks outward through every enclosing `{`, checking the header in front of each one, so a call
    // nested inside a lambda inside a development-only `if` is still recognised.
    private static bool IsInsideDevelopmentBlock(string code, int callIndex)
    {
        var depth = 0;

        for (var i = callIndex - 1; i >= 0; i--)
        {
            switch (code[i])
            {
                case '}':
                    depth++;
                    break;

                case '{' when depth > 0:
                    depth--;
                    break;

                case '{':
                    var headerStart = i == 0 ? 0 : code.LastIndexOfAny([';', '{', '}'], i - 1) + 1;

                    if (IsDevelopmentIfHeader(code[headerStart..i]))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool IsInsideDebugDirective(string code, int callIndex)
    {
        var before = code[..callIndex];
        var debugIf = DebugDirective().Matches(before).LastOrDefault();

        return debugIf is not null && before.IndexOf("#endif", debugIf.Index, StringComparison.Ordinal) < 0
            && before.IndexOf("#else", debugIf.Index, StringComparison.Ordinal) < 0;
    }

    private static bool IsDevelopmentIfHeader(string header) =>
        IfKeyword().IsMatch(header) && IsDevelopmentCheck(header);

    // A positive check only: `!env.IsDevelopment()` guards the opposite case.
    private static bool IsDevelopmentCheck(string text) =>
        DevelopmentCheck().IsMatch(text) && !NegatedDevelopmentCheck().IsMatch(text);

    // The text between the call's parentheses, honouring nested parentheses.
    private static string ReadArgument(string code, int openParenEnd)
    {
        var depth = 1;

        for (var i = openParenEnd; i < code.Length; i++)
        {
            depth += code[i] switch { '(' => 1, ')' => -1, _ => 0 };

            if (depth == 0)
            {
                return code[openParenEnd..i];
            }
        }

        return string.Empty;
    }

    [GeneratedRegex(@"\bEnableSensitiveDataLogging\s*\(")]
    private static partial Regex SensitiveDataLoggingCall();

    [GeneratedRegex(@"\bIsDevelopment\s*\(\s*\)|\bIsEnvironment\s*\(\s*""Development""\s*\)")]
    private static partial Regex DevelopmentCheck();

    [GeneratedRegex(@"!\s*[\w.]*(?:IsDevelopment\s*\(|IsEnvironment\s*\(\s*""Development"")")]
    private static partial Regex NegatedDevelopmentCheck();

    [GeneratedRegex(@"\bif\s*\(")]
    private static partial Regex IfKeyword();

    [GeneratedRegex(@"^[ \t]*#if[ \t]+DEBUG\b", RegexOptions.Multiline)]
    private static partial Regex DebugDirective();
}
