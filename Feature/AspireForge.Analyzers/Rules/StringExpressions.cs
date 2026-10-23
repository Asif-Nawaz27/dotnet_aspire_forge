using System.Text.RegularExpressions;

namespace AspireForge.Analyzers.Rules;

// Recognises C# string expressions built from runtime values at the call site - shared by rules that
// care whether text is a fixed template or has values pasted into it (SEC011 for SQL, OBS006 for log
// messages). Works on comment-stripped source text; it's a small scanner, not a C# parser.
internal static partial class StringExpressions
{
    // True for:
    //   - string.Format(...)
    //   - an interpolated string with at least one hole: $"... {x}", $@"...", $"""...""" (unless
    //     interpolationIsSafe - e.g. EF Core's FromSql turns holes into parameters)
    //   - a literal concatenated with a non-literal: "..." + x. Literal + literal is constant: false.
    public static bool IsBuiltFromValues(string expression, bool interpolationIsSafe = false)
    {
        var text = expression.TrimStart();

        if (FormatCall().IsMatch(text))
        {
            return true;
        }

        if (text.StartsWith('$') || text.StartsWith("@$", StringComparison.Ordinal))
        {
            return !interpolationIsSafe && HasInterpolationHole(text);
        }

        return IsLiteralConcatenatedWithValue(text);
    }

    // Whether an argument is a string expression at all - a literal (regular, verbatim, raw, or
    // interpolated) or string.Format - as opposed to a variable, exception, or enum value.
    public static bool IsStringExpression(string expression)
    {
        var text = expression.TrimStart();
        return text.StartsWith('"') || text.StartsWith('$') || text.StartsWith('@') || FormatCall().IsMatch(text);
    }

    // Splits a call's argument list at top-level commas - not those inside nested (), [], {}, or string
    // literals (so a message like "Order {Id}, total {Total}" stays one argument).
    public static IReadOnlyList<string> SplitArguments(string arguments)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < arguments.Length; i++)
        {
            var c = arguments[i];

            if (c == '"')
            {
                var prefixStart = i;
                while (prefixStart > start && arguments[prefixStart - 1] is '$' or '@')
                {
                    prefixStart--;
                }

                var end = SkipStringLiteral(arguments, i, verbatim: arguments[prefixStart..i].Contains('@'));
                if (end < 0)
                {
                    break;
                }

                i = end - 1;
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                result.Add(arguments[start..i]);
                start = i + 1;
            }
        }

        result.Add(arguments[start..]);
        return result;
    }

    // `{x}` is a hole; `{{` is an escaped brace. For raw strings with N leading '$', holes use N braces.
    private static bool HasInterpolationHole(string text)
    {
        var prefixLength = text.TakeWhile(c => c is '$' or '@').Count();
        var dollars = text[..prefixLength].Count(c => c == '$');
        var literalEnd = SkipStringLiteral(text, prefixLength, verbatim: text[..prefixLength].Contains('@'));
        var literal = literalEnd > 0 ? text[..literalEnd] : text;

        return dollars >= 2
            ? literal.Contains(new string('{', dollars), StringComparison.Ordinal)
            : Regex.IsMatch(literal, @"(?<!\{)\{(?!\{)");
    }

    // "..." + value  - but not "..." + "..." (constant).
    private static bool IsLiteralConcatenatedWithValue(string text)
    {
        var start = text.StartsWith('@') ? 1 : 0;

        if (start >= text.Length || text[start] != '"')
        {
            return false;
        }

        var end = SkipStringLiteral(text, start, verbatim: start == 1);

        while (end > 0)
        {
            var rest = text[end..].TrimStart();

            if (!rest.StartsWith('+'))
            {
                return false;
            }

            rest = rest[1..].TrimStart();
            var next = rest.StartsWith('@') && rest.Length > 1 && rest[1] == '"' ? 1 : 0;

            if (next >= rest.Length || rest[next] != '"')
            {
                return true; // concatenated with something that isn't a plain literal
            }

            var offset = text.Length - rest.Length;
            end = SkipStringLiteral(text, offset + next, verbatim: next == 1);
        }

        return false;
    }

    // Given the index of a literal's opening quote (after any $/@ prefix), returns the index just past
    // its closing quote, or -1. Handles regular ("\"" escapes), verbatim (@"" "" escapes), and raw
    // ("""...""") literals.
    private static int SkipStringLiteral(string text, int quoteIndex, bool verbatim)
    {
        if (quoteIndex >= text.Length || text[quoteIndex] != '"')
        {
            return -1;
        }

        var quotes = 0;
        while (quoteIndex + quotes < text.Length && text[quoteIndex + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3)
        {
            var close = text.IndexOf(new string('"', quotes), quoteIndex + quotes, StringComparison.Ordinal);
            return close < 0 ? -1 : close + quotes;
        }

        if (quotes == 2 && !verbatim)
        {
            return quoteIndex + 2; // empty string ""
        }

        for (var i = quoteIndex + 1; i < text.Length; i++)
        {
            if (!verbatim && text[i] == '\\')
            {
                i++;
                continue;
            }

            if (text[i] == '"')
            {
                if (verbatim && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                return i + 1;
            }
        }

        return -1;
    }

    [GeneratedRegex(@"^(?:string|String)\.Format\s*\(")]
    private static partial Regex FormatCall();
}
