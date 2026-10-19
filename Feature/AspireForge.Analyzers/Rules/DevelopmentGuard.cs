using System.Text.RegularExpressions;

namespace AspireForge.Analyzers.Rules;

// Decides whether a position in (comment-stripped) source only runs in development, for rules that
// allow something locally but not in production (OBS004, SEC005). Recognised guards:
//   - an enclosing `if (...IsDevelopment()...)` - braced or braceless, at any nesting depth - that
//     isn't negated
//   - an enclosing `#if DEBUG` (before any #else/#endif)
// This is a text heuristic, not data-flow analysis: a guard through a variable (`var dev =
// env.IsDevelopment(); if (dev) ...`) isn't recognised.
internal static partial class DevelopmentGuard
{
    public static bool Covers(string code, int index) =>
        IsInsideDebugDirective(code, index)
        || IsInBracelessDevelopmentIf(code, index)
        || IsInsideDevelopmentBlock(code, index);

    // A positive check only: `!env.IsDevelopment()` guards the opposite case.
    public static bool IsDevelopmentCheck(string text) =>
        DevelopmentCheck().IsMatch(text) && !NegatedDevelopmentCheck().IsMatch(text);

    // `if (env.IsDevelopment()) options.EnableSensitiveDataLogging();` - the statement header is
    // everything since the previous statement or block boundary.
    private static bool IsInBracelessDevelopmentIf(string code, int index)
    {
        var start = code.LastIndexOfAny([';', '{', '}'], Math.Max(0, index - 1)) + 1;
        return IsDevelopmentIfHeader(code[start..index]);
    }

    // Walks outward through every enclosing `{`, checking the header in front of each one, so code
    // nested inside a lambda or object initializer inside a development-only `if` is still recognised.
    private static bool IsInsideDevelopmentBlock(string code, int index)
    {
        var depth = 0;

        for (var i = index - 1; i >= 0; i--)
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

    private static bool IsInsideDebugDirective(string code, int index)
    {
        var before = code[..index];
        var debugIf = DebugDirective().Matches(before).LastOrDefault();

        return debugIf is not null && before.IndexOf("#endif", debugIf.Index, StringComparison.Ordinal) < 0
            && before.IndexOf("#else", debugIf.Index, StringComparison.Ordinal) < 0;
    }

    private static bool IsDevelopmentIfHeader(string header) =>
        IfKeyword().IsMatch(header) && IsDevelopmentCheck(header);

    [GeneratedRegex(@"\bIsDevelopment\s*\(\s*\)|\bIsEnvironment\s*\(\s*""Development""\s*\)")]
    private static partial Regex DevelopmentCheck();

    [GeneratedRegex(@"!\s*[\w.]*(?:IsDevelopment\s*\(|IsEnvironment\s*\(\s*""Development"")")]
    private static partial Regex NegatedDevelopmentCheck();

    [GeneratedRegex(@"\bif\s*\(")]
    private static partial Regex IfKeyword();

    [GeneratedRegex(@"^[ \t]*#if[ \t]+DEBUG\b", RegexOptions.Multiline)]
    private static partial Regex DebugDirective();
}
