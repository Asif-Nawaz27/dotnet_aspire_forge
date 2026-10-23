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

                if (StringExpressions.IsBuiltFromValues(argument, interpolationIsSafe))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index), api));
                }
            }

            foreach (Match assignment in CommandTextAssignment().Matches(code))
            {
                var value = code[(assignment.Index + assignment.Length)..];
                var end = value.IndexOf(';');

                if (StringExpressions.IsBuiltFromValues(end >= 0 ? value[..end] : value, interpolationIsSafe: false))
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

    [GeneratedRegex(@"(?:\.(?<api>FromSqlRaw|ExecuteSqlRawAsync|ExecuteSqlRaw|SqlQueryRaw|FromSql|ExecuteSqlAsync|ExecuteSql|SqlQuery|ExecuteSqlCommandAsync|ExecuteSqlCommand|QueryFirstOrDefaultAsync|QueryFirstOrDefault|QueryFirstAsync|QueryFirst|QuerySingleOrDefaultAsync|QuerySingleOrDefault|QuerySingleAsync|QuerySingle|QueryMultipleAsync|QueryMultiple|QueryAsync|Query|ExecuteScalarAsync|ExecuteScalar|ExecuteReaderAsync|ExecuteReader|ExecuteAsync|Execute)|\bnew\s+(?<api>(?:Sql|Npgsql|MySql|Sqlite|Oracle|Odbc|OleDb)Command))\s*(?:<[^>()]*>)?\s*\(")]
    private static partial Regex RawSqlCall();

    [GeneratedRegex(@"^(?:Query|Execute)(?!Sql)")]
    private static partial Regex DapperMethod();

    [GeneratedRegex(@"^(?:FromSql|ExecuteSqlAsync|ExecuteSql|SqlQuery|ExecuteSqlCommandAsync|ExecuteSqlCommand)$")]
    private static partial Regex SafeWithInterpolation();

    [GeneratedRegex(@"\bCommandText\s*=(?!=)\s*")]
    private static partial Regex CommandTextAssignment();

    [GeneratedRegex(@"^\s*using\s+Dapper\s*;", RegexOptions.Multiline)]
    private static partial Regex UsingDapper();
}
