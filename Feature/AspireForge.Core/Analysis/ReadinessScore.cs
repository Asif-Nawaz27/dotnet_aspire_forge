namespace AspireForge.Core.Analysis;

// Production readiness, 0-100: start at 100 and subtract points per finding, weighted by severity.
// It depends only on the findings - not on how many rules passed - so adding rules can't inflate it,
// and ten warnings always cost the same however large the rule set grows.
//
//   critical 30 · error 15 · warning 5 · suggestion 2 · info 1
//
// Caps: any error limits the score to 60, any critical to 30 - a project with an open error isn't
// production-ready, however clean the rest is. OutOfTen rounds DOWN, so 10/10 means nothing to fix.
public sealed record ReadinessScore(int Points, int Penalty, string? CapReason)
{
    public const int ErrorCap = 60;
    public const int CriticalCap = 30;

    public int OutOfTen => Points / 10;

    public static int PenaltyFor(Severity severity) => severity switch
    {
        Severity.Critical => 30,
        Severity.Error => 15,
        Severity.Warning => 5,
        Severity.Suggestion => 2,
        _ => 1,
    };

    public static ReadinessScore Calculate(IEnumerable<AnalysisIssue> issues)
    {
        var findings = issues.ToList();
        var penalty = findings.Sum(issue => PenaltyFor(issue.Severity));
        var points = Math.Max(0, 100 - penalty);

        var criticals = findings.Count(issue => issue.Severity == Severity.Critical);
        var errors = findings.Count(issue => issue.Severity == Severity.Error);

        if (criticals > 0 && points > CriticalCap)
        {
            return new ReadinessScore(CriticalCap, penalty, $"capped at {CriticalCap} by {Count(criticals, "critical finding")}");
        }

        if (errors > 0 && points > ErrorCap)
        {
            return new ReadinessScore(ErrorCap, penalty, $"capped at {ErrorCap} by {Count(errors, "error")}");
        }

        return new ReadinessScore(points, penalty, CapReason: null);
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
}
