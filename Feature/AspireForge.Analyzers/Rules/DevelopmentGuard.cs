using System.Text.RegularExpressions;

namespace AspireForge.Analyzers.Rules;

// Decides whether a position in (comment-stripped) source only runs in development, for rules that
// allow something locally but not in production (OBS004, SEC005, SEC007, SEC010, PERF004). Recognised
// guards, at any nesting depth:
//   - an enclosing `if (...IsDevelopment()...)` - braced or braceless - that isn't negated
//   - the `else` of `if (!...IsDevelopment())` - the ASP.NET Core template shape:
//       if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); }
//       else { app.UseDeveloperExceptionPage(); }
//   - an enclosing `#if DEBUG` (before any #else/#endif)
// With allowNonProduction, "anywhere but production" also counts - `if (!env.IsProduction())`, or the
// else of `if (env.IsProduction())` - for rules whose concern is production specifically (SEC007).
// This is a text heuristic, not data-flow analysis: a guard through a variable (`var dev =
// env.IsDevelopment(); if (dev) ...`) isn't recognised.
internal static partial class DevelopmentGuard
{
    public static bool Covers(string code, int index, bool allowNonProduction = false) =>
        IsInsideDebugDirective(code, index)
        || IsInBracelessGuard(code, index, allowNonProduction)
        || IsInsideGuardedBlock(code, index, allowNonProduction);

    // A positive check only: `!env.IsDevelopment()` guards the opposite case.
    public static bool IsDevelopmentCheck(string text) =>
        DevelopmentCheck().IsMatch(text) && !NegatedDevelopmentCheck().IsMatch(text);

    // The condition limits its body to development (or, if allowed, to non-production).
    private static bool IsGuardCondition(string text, bool allowNonProduction) =>
        IsDevelopmentCheck(text)
        || (allowNonProduction && NegatedProductionCheck().IsMatch(text));

    // The condition's ELSE branch is limited to development (or, if allowed, to non-production).
    private static bool IsInverseGuardCondition(string text, bool allowNonProduction) =>
        NegatedDevelopmentCheck().IsMatch(text)
        || (allowNonProduction && ProductionCheck().IsMatch(text) && !NegatedProductionCheck().IsMatch(text));

    // `if (env.IsDevelopment()) options.X();` or `... else options.X();` - the statement header is
    // everything since the previous statement or block boundary.
    private static bool IsInBracelessGuard(string code, int index, bool allowNonProduction)
    {
        var start = code.LastIndexOfAny([';', '{', '}'], Math.Max(0, index - 1)) + 1;
        var header = code[start..index];

        if (IfKeyword().IsMatch(header) && IsGuardCondition(header, allowNonProduction))
        {
            return true;
        }

        return ElseKeyword().IsMatch(header) && IsElseOfInverseGuard(code, start - 1, allowNonProduction);
    }

    // Walks outward through every enclosing `{`, checking the header in front of each one, so code
    // nested inside a lambda or object initializer inside a guarded block is still recognised.
    private static bool IsInsideGuardedBlock(string code, int index, bool allowNonProduction)
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
                    var header = code[headerStart..i];

                    if (IfKeyword().IsMatch(header) && IsGuardCondition(header, allowNonProduction))
                    {
                        return true;
                    }

                    if (ElseKeyword().IsMatch(header) && IsElseOfInverseGuard(code, headerStart - 1, allowNonProduction))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    // `boundary` is the character just before an `else`: the `}` closing a braced if-body, or the `;`
    // ending a braceless one. Finds that if's condition and checks whether its else is the guarded side.
    private static bool IsElseOfInverseGuard(string code, int boundary, bool allowNonProduction)
    {
        if (boundary < 0)
        {
            return false;
        }

        int ifHeaderEnd;

        if (code[boundary] == '}')
        {
            var depth = 0;
            ifHeaderEnd = -1;

            for (var i = boundary; i >= 0; i--)
            {
                if (code[i] == '}')
                {
                    depth++;
                }
                else if (code[i] == '{' && --depth == 0)
                {
                    ifHeaderEnd = i;
                    break;
                }
            }

            if (ifHeaderEnd < 0)
            {
                return false;
            }
        }
        else if (code[boundary] == ';')
        {
            ifHeaderEnd = boundary;
        }
        else
        {
            return false;
        }

        var ifHeaderStart = ifHeaderEnd == 0 ? 0 : code.LastIndexOfAny([';', '{', '}'], ifHeaderEnd - 1) + 1;
        var ifHeader = code[ifHeaderStart..ifHeaderEnd];

        return IfKeyword().IsMatch(ifHeader) && IsInverseGuardCondition(ifHeader, allowNonProduction);
    }

    private static bool IsInsideDebugDirective(string code, int index)
    {
        var before = code[..index];
        var debugIf = DebugDirective().Matches(before).LastOrDefault();

        return debugIf is not null && before.IndexOf("#endif", debugIf.Index, StringComparison.Ordinal) < 0
            && before.IndexOf("#else", debugIf.Index, StringComparison.Ordinal) < 0;
    }

    [GeneratedRegex(@"\bIsDevelopment\s*\(\s*\)|\bIsEnvironment\s*\(\s*""Development""\s*\)")]
    private static partial Regex DevelopmentCheck();

    [GeneratedRegex(@"!\s*[\w.]*(?:IsDevelopment\s*\(|IsEnvironment\s*\(\s*""Development"")")]
    private static partial Regex NegatedDevelopmentCheck();

    [GeneratedRegex(@"\bIsProduction\s*\(\s*\)|\bIsEnvironment\s*\(\s*""Production""\s*\)")]
    private static partial Regex ProductionCheck();

    [GeneratedRegex(@"!\s*[\w.]*(?:IsProduction\s*\(|IsEnvironment\s*\(\s*""Production"")")]
    private static partial Regex NegatedProductionCheck();

    [GeneratedRegex(@"\bif\s*\(")]
    private static partial Regex IfKeyword();

    // A header that is just `else` (not `else if`, whose own condition is checked as an if).
    [GeneratedRegex(@"^\s*else\b(?!\s+if\b)")]
    private static partial Regex ElseKeyword();

    [GeneratedRegex(@"^[ \t]*#if[ \t]+DEBUG\b", RegexOptions.Multiline)]
    private static partial Regex DebugDirective();
}
