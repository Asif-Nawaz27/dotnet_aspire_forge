using System.Security.Cryptography;
using System.Text;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Suppressions;

public enum AcceptedBy
{
    InlineComment,
    Baseline,
}

// A finding left out of the report, kept so the report can say what it left out. Line is null for a
// finding about the project as a whole.
public sealed record AcceptedFinding(string RuleId, string? File, int? Line, AcceptedBy By, string? Reason = null);

// Inline comments first, then the baseline. Holds per-run state (baseline entries get used up), so
// create one per analysis. Rules run concurrently, hence the lock.
public sealed class FindingSuppressions : IFindingSuppressions
{
    private readonly object _gate = new();
    private readonly string _rootDirectory;
    private readonly bool _recording;
    private readonly Dictionary<EntryKey, int> _unmatched = [];
    private readonly Dictionary<EntryKey, int> _recorded = [];
    private readonly Dictionary<string, SourceLines> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AcceptedFinding> _accepted = [];

    private FindingSuppressions(string rootDirectory, BaselineFile? baseline, bool recording)
    {
        _rootDirectory = rootDirectory;
        _recording = recording;

        foreach (var entry in baseline?.Findings ?? [])
        {
            var key = new EntryKey(entry.RuleId, entry.File, entry.Fingerprint);
            _unmatched[key] = _unmatched.GetValueOrDefault(key) + entry.Count;
        }
    }

    public static FindingSuppressions Create(string rootDirectory, BaselineFile? baseline) =>
        new(rootDirectory, baseline, recording: false);

    // Suppresses nothing but inline comments, and notes everything else for RecordedEntries - which is
    // how a baseline is built: run the rules once and keep what they would have reported.
    public static FindingSuppressions Recording(string rootDirectory) =>
        new(rootDirectory, baseline: null, recording: true);

    public IReadOnlyList<AcceptedFinding> Accepted
    {
        get
        {
            lock (_gate)
            {
                return _accepted
                    .OrderBy(finding => finding.RuleId, StringComparer.Ordinal)
                    .ThenBy(finding => finding.File, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(finding => finding.Line)
                    .ToList();
            }
        }
    }

    public IReadOnlyList<BaselineEntry> RecordedEntries
    {
        get
        {
            lock (_gate)
            {
                return _recorded
                    .Select(entry => new BaselineEntry(entry.Key.RuleId, entry.Key.File, entry.Key.Fingerprint, entry.Value))
                    .ToList();
            }
        }
    }

    // Accepted occurrences that no longer exist - usually because they were fixed. Only counts rules
    // that ran: a disabled rule, or DEP002 without --vulnerable, had no chance to match its entries.
    public int UnmatchedBaselineCount(IReadOnlySet<string> rulesThatRan)
    {
        lock (_gate)
        {
            return _unmatched.Where(entry => rulesThatRan.Contains(entry.Key.RuleId)).Sum(entry => entry.Value);
        }
    }

    public bool IsSuppressed(string ruleId, string file, int? line)
    {
        if (line is not { } lineNumber)
        {
            return false;
        }

        lock (_gate)
        {
            var source = LinesOf(file);

            if (source.Suppressions.TryGetValue(lineNumber, out var comments)
                && comments.FirstOrDefault(comment => comment.RuleIds.Contains(ruleId)) is { } comment)
            {
                _accepted.Add(new AcceptedFinding(ruleId, file, lineNumber, AcceptedBy.InlineComment, comment.Reason));
                return true;
            }

            var text = lineNumber >= 1 && lineNumber <= source.Lines.Length ? source.Lines[lineNumber - 1] : string.Empty;

            return MatchBaseline(new EntryKey(ruleId, RelativePath(file), Fingerprint(text.Trim())), file, lineNumber);
        }
    }

    public bool IsSuppressed(AnalysisIssue issue)
    {
        lock (_gate)
        {
            var file = issue.FilePath is null ? string.Empty : RelativePath(issue.FilePath);

            // The description, because it's what names the specifics: a newly vulnerable package changes
            // DEP002's description, so it surfaces as new instead of hiding behind the old entry.
            return MatchBaseline(new EntryKey(issue.RuleId, file, Fingerprint(issue.Description)), file: null, line: null);
        }
    }

    public static string Fingerprint(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    private bool MatchBaseline(EntryKey key, string? file, int? line)
    {
        if (_recording)
        {
            _recorded[key] = _recorded.GetValueOrDefault(key) + 1;
            return false;
        }

        if (_unmatched.GetValueOrDefault(key) == 0)
        {
            return false;
        }

        _unmatched[key]--;
        _accepted.Add(new AcceptedFinding(key.RuleId, file, line, AcceptedBy.Baseline));
        return true;
    }

    private SourceLines LinesOf(string file)
    {
        if (!_files.TryGetValue(file, out var source))
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file) : [];
            source = new SourceLines(lines, InlineSuppressions.Parse(lines));
            _files[file] = source;
        }

        return source;
    }

    // Forward slashes, so a baseline written on Windows matches on a Linux CI runner.
    private string RelativePath(string file) => Path.GetRelativePath(_rootDirectory, file).Replace('\\', '/');

    private sealed record SourceLines(string[] Lines, IReadOnlyDictionary<int, IReadOnlyList<InlineSuppression>> Suppressions);

    private readonly record struct EntryKey(string RuleId, string File, string Fingerprint);
}
