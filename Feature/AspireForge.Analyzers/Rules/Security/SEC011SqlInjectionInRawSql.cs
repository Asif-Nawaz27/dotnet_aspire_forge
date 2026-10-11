using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// Flags SQL built from runtime values at the call site of an API that executes it verbatim - the
// classic injection shape. The value is pasted into the SQL text, so input like `' OR 1=1 --` changes
// the query instead of being treated as data.
//
// Unsafe argument shapes (first argument only):
//   - an interpolated string with holes:   FromSqlRaw($"... WHERE Name = '{name}'")
//   - string.Format:                        ExecuteSqlRaw(string.Format("... {0}", id))
//   - a literal concatenated with a value:  new SqlCommand("... WHERE Id = " + id, conn)
// Concatenating two literals ("SELECT a " + "FROM b") is constant and not flagged.
//
// APIs checked:
//   - EF Core raw APIs - FromSqlRaw, ExecuteSqlRaw(Async), SqlQueryRaw - for all three shapes.
//   - EF Core FromSql, ExecuteSql(Async), SqlQuery, ExecuteSqlCommand(Async) - concatenation and
//     string.Format only. With an interpolated string these take a FormattableString and turn every
//     hole into a DbParameter, which is exactly the safe fix, so interpolation is fine there.
//   - ADO.NET - new SqlCommand/NpgsqlCommand/...(sql, ...) and CommandText = sql.
//   - Dapper (Query*, Execute*) in files that import Dapper - it sends the string as-is.
// Not flagged: a variable passed in (FromSqlRaw(sql)) - where it was built isn't visible to a text
// heuristic - and parameterised calls such as FromSqlRaw("... {0}", id).
public sealed partial class SEC011SqlInjectionInRawSql : IAnalysisRule
{
    private const int MaxLocationsInDescription = 3;

    public string Id => "SEC011";

    public string Title => "SQL injection risk in raw SQL";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line, string Api)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);
            var usesDapper = UsingDapper().IsMatch(code);

            foreach (Match call in RawSqlCall().Matches(code))
            {
                var api = call.Groups["api"].Value;
                var isDapperCall = DapperMethod().IsMatch(api);

                if (isDapperCall && !usesDapper)
                {
                    continue;
                }

                // EF Core's non-Raw APIs parameterise interpolated strings themselves.
                var interpolationIsSafe = SafeWithInterpolation().IsMatch(api);
                var argument = SourceFileHeuristics.ReadCallArguments(code, call.Index + call.Length);

                if (IsUnsafeSql(argument, interpolationIsSafe))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index), api));
                }
            }

            foreach (Match assignment in CommandTextAssignment().Matches(code))
            {
                var value = code[(assignment.Index + assignment.Length)..];
                var end = value.IndexOf(';');

                if (IsUnsafeSql(end >= 0 ? value[..end] : value, interpolationIsSafe: false))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, assignment.Index), "CommandText"));
                }
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings.OrderBy(f => f.File, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Line).ToList();

        var locations = string.Join(
            ", ",
            findings.Take(MaxLocationsInDescription).Select(f => $"{f.Api} at {Path.GetFileName(f.File)}:{f.Line}"));
        var more = findings.Count > MaxLocationsInDescription
            ? $" (and {findings.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"SQL is built from runtime values and executed verbatim ({locations}{more}). Input containing quotes "
                + "or SQL keywords changes the query itself. Use parameters instead: EF Core's FromSql($\"...\") / "
                + "ExecuteSql($\"...\") / SqlQuery($\"...\") turn every interpolation hole into a parameter; with "
                + "ADO.NET add values via command.Parameters; with Dapper pass an object (new { id }).",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    private static bool IsUnsafeSql(string arguments, bool interpolationIsSafe)
    {
        var text = arguments.TrimStart();

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

    // `{x}` is a hole; `{{` is an escaped brace. For raw strings with N leading '$', holes use N braces.
    private static bool HasInterpolationHole(string text)
    {
        var dollars = text.TakeWhile(c => c is '$' or '@').Count(c => c == '$');
        var literalEnd = SkipStringLiteral(text, text.TakeWhile(c => c is '$' or '@').Count());
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

        var end = SkipStringLiteral(text, start);

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
            end = SkipStringLiteral(text, offset + next);
        }

        return false;
    }

    // Given the index of a literal's opening quote (after any $/@ prefix), returns the index just past
    // its closing quote, or -1. Handles regular ("\"" escapes), verbatim (@"" "" escapes), and raw
    // ("""...""") literals.
    private static int SkipStringLiteral(string text, int quoteIndex)
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

        if (quotes == 2)
        {
            return quoteIndex + 2; // empty string ""
        }

        var verbatim = quoteIndex > 0 && text[..quoteIndex].Contains('@');

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

    [GeneratedRegex(@"(?:\.(?<api>FromSqlRaw|ExecuteSqlRawAsync|ExecuteSqlRaw|SqlQueryRaw|FromSql|ExecuteSqlAsync|ExecuteSql|SqlQuery|ExecuteSqlCommandAsync|ExecuteSqlCommand|QueryFirstOrDefaultAsync|QueryFirstOrDefault|QueryFirstAsync|QueryFirst|QuerySingleOrDefaultAsync|QuerySingleOrDefault|QuerySingleAsync|QuerySingle|QueryMultipleAsync|QueryMultiple|QueryAsync|Query|ExecuteScalarAsync|ExecuteScalar|ExecuteReaderAsync|ExecuteReader|ExecuteAsync|Execute)|\bnew\s+(?<api>(?:Sql|Npgsql|MySql|Sqlite|Oracle|Odbc|OleDb)Command))\s*(?:<[^>()]*>)?\s*\(")]
    private static partial Regex RawSqlCall();

    [GeneratedRegex(@"^(?:Query|Execute)(?!Sql)")]
    private static partial Regex DapperMethod();

    [GeneratedRegex(@"^(?:FromSql|ExecuteSqlAsync|ExecuteSql|SqlQuery|ExecuteSqlCommandAsync|ExecuteSqlCommand)$")]
    private static partial Regex SafeWithInterpolation();

    [GeneratedRegex(@"\bCommandText\s*=(?!=)\s*")]
    private static partial Regex CommandTextAssignment();

    [GeneratedRegex(@"^(?:string|String)\.Format\s*\(")]
    private static partial Regex FormatCall();

    [GeneratedRegex(@"^\s*using\s+Dapper\s*;", RegexOptions.Multiline)]
    private static partial Regex UsingDapper();
}
