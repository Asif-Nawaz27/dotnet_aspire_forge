namespace AspireForge.Core.Analysis;

// Decides whether a finding has been accepted - by an inline comment or the baseline - and so shouldn't
// be reported.
//
// A rule folds every occurrence it finds into one AnalysisIssue, so suppression has to happen per
// occurrence, before that issue is built. Filtering the finished issue would hide every occurrence
// because of a comment on the first one. Rules therefore ask about each located occurrence themselves;
// ProjectAnalyzer asks about issues that have no line.
public interface IFindingSuppressions
{
    // One occurrence at a known location. Call this once per occurrence: a baseline entry covers a
    // fixed number of them. A null line can't be matched, so it's never suppressed.
    bool IsSuppressed(string ruleId, string file, int? line);

    // A finding about the project as a whole, which has no line to attach a comment or fingerprint to.
    bool IsSuppressed(AnalysisIssue issue);
}

public sealed class NoFindingSuppressions : IFindingSuppressions
{
    public static readonly NoFindingSuppressions Instance = new();

    private NoFindingSuppressions()
    {
    }

    public bool IsSuppressed(string ruleId, string file, int? line) => false;

    public bool IsSuppressed(AnalysisIssue issue) => false;
}
