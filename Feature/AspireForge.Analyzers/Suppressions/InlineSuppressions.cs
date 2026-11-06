using System.Text.RegularExpressions;

namespace AspireForge.Analyzers.Suppressions;

public sealed record InlineSuppression(IReadOnlySet<string> RuleIds, string? Reason);

// Reads suppression comments from a file's lines:
//
//   // aspireforge-disable-next-line SEC010: self-signed certificate on the internal broker
//   client.Connect(); // aspireforge-disable-line PERF003, OBS005
//
// The same comment works with # (Dockerfile) and <!-- --> (project files), so every file a rule reports
// on can carry one. The reason follows a colon rather than the more usual "--" because XML forbids "--"
// inside a comment: it would stop the project file from loading. Rule IDs are required, so a comment
// can't silence everything on a line.
public static partial class InlineSuppressions
{
    // Keyed by the 1-based line each comment applies to.
    public static IReadOnlyDictionary<int, IReadOnlyList<InlineSuppression>> Parse(IReadOnlyList<string> lines)
    {
        var suppressions = new Dictionary<int, IReadOnlyList<InlineSuppression>>();

        for (var i = 0; i < lines.Count; i++)
        {
            var match = Comment().Match(lines[i]);

            if (!match.Success)
            {
                continue;
            }

            var ruleIds = match.Groups["ids"].Value
                .Split(',', StringSplitOptions.TrimEntries)
                .Select(id => id.ToUpperInvariant())
                .ToHashSet();
            var reason = match.Groups["reason"].Value.Trim();
            var target = match.Groups["kind"].Value == "next-line" ? i + 2 : i + 1;
            var suppression = new InlineSuppression(ruleIds, reason.Length == 0 ? null : reason);

            suppressions[target] = suppressions.TryGetValue(target, out var existing)
                ? [.. existing, suppression]
                : [suppression];
        }

        return suppressions;
    }

    [GeneratedRegex(@"(?://|#|<!--)\s*aspireforge-disable-(?<kind>next-line|line)\s+(?<ids>[A-Za-z]+\d+(?:\s*,\s*[A-Za-z]+\d+)*)\s*(?::(?<reason>.*?))?\s*(?:-->)?\s*$")]
    private static partial Regex Comment();
}
