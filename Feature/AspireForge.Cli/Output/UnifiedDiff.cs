namespace AspireForge.Cli.Output;

// A unified diff, as `git diff` prints it, so a preview reads the way a developer expects.
public static class UnifiedDiff
{
    private const int ContextLines = 3;

    // Above this many line pairs the changed region is shown as replaced rather than compared line by
    // line. Comparing costs memory proportional to this, and fixes make small edits to small files.
    private const long MaxComparedPairs = 4_000_000;

    private enum Kind
    {
        Same,
        Removed,
        Added,
    }

    private readonly record struct Line(Kind Kind, string Text, int OldNumber, int NewNumber);

    public static IReadOnlyList<string> Build(string path, string before, string after)
    {
        var lines = Compare(Split(before), Split(after));
        var output = new List<string>();

        if (lines.TrueForAll(line => line.Kind == Kind.Same))
        {
            return output;
        }

        output.Add($"--- {path}");
        output.Add($"+++ {path}");

        foreach (var (start, end) in Hunks(lines))
        {
            var hunk = lines[start..(end + 1)];
            var oldCount = hunk.Count(line => line.Kind != Kind.Added);
            var newCount = hunk.Count(line => line.Kind != Kind.Removed);

            output.Add($"@@ -{hunk[0].OldNumber},{oldCount} +{hunk[0].NewNumber},{newCount} @@");
            output.AddRange(hunk.Select(line => Prefix(line.Kind) + line.Text));
        }

        return output;
    }

    // Each run of changes with its context. Changes close enough for their context to touch share a hunk.
    private static IEnumerable<(int Start, int End)> Hunks(List<Line> lines)
    {
        var changes = Enumerable.Range(0, lines.Count).Where(i => lines[i].Kind != Kind.Same).ToList();
        var start = changes[0];
        var end = changes[0];

        foreach (var change in changes.Skip(1))
        {
            if (change - end > ContextLines * 2)
            {
                yield return (Math.Max(0, start - ContextLines), Math.Min(lines.Count - 1, end + ContextLines));
                start = change;
            }

            end = change;
        }

        yield return (Math.Max(0, start - ContextLines), Math.Min(lines.Count - 1, end + ContextLines));
    }

    private static List<Line> Compare(string[] before, string[] after)
    {
        var prefix = 0;

        while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix])
        {
            prefix++;
        }

        var suffix = 0;

        while (suffix < before.Length - prefix
               && suffix < after.Length - prefix
               && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
        {
            suffix++;
        }

        var kinds = new List<Kind>(Enumerable.Repeat(Kind.Same, prefix));
        kinds.AddRange(CompareMiddle(before[prefix..^suffix], after[prefix..^suffix]));
        kinds.AddRange(Enumerable.Repeat(Kind.Same, suffix));

        return Number(kinds, before, after);
    }

    // Longest common subsequence: table[i, j] is how many lines the tails from i and j have in common.
    private static List<Kind> CompareMiddle(string[] before, string[] after)
    {
        var kinds = new List<Kind>();

        if ((long)before.Length * after.Length > MaxComparedPairs)
        {
            kinds.AddRange(Enumerable.Repeat(Kind.Removed, before.Length));
            kinds.AddRange(Enumerable.Repeat(Kind.Added, after.Length));
            return kinds;
        }

        var table = new int[before.Length + 1, after.Length + 1];

        for (var i = before.Length - 1; i >= 0; i--)
        {
            for (var j = after.Length - 1; j >= 0; j--)
            {
                table[i, j] = before[i] == after[j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        var (x, y) = (0, 0);

        while (x < before.Length || y < after.Length)
        {
            if (x < before.Length && y < after.Length && before[x] == after[y])
            {
                kinds.Add(Kind.Same);
                x++;
                y++;
            }
            else if (y < after.Length && (x == before.Length || table[x, y + 1] >= table[x + 1, y]))
            {
                kinds.Add(Kind.Added);
                y++;
            }
            else
            {
                kinds.Add(Kind.Removed);
                x++;
            }
        }

        return kinds;
    }

    private static List<Line> Number(List<Kind> kinds, string[] before, string[] after)
    {
        var lines = new List<Line>(kinds.Count);
        var (oldIndex, newIndex) = (0, 0);

        foreach (var kind in kinds)
        {
            var text = kind == Kind.Added ? after[newIndex] : before[oldIndex];

            lines.Add(new Line(kind, text, oldIndex + 1, newIndex + 1));

            oldIndex += kind == Kind.Added ? 0 : 1;
            newIndex += kind == Kind.Removed ? 0 : 1;
        }

        return lines;
    }

    private static string Prefix(Kind kind) => kind switch
    {
        Kind.Added => "+",
        Kind.Removed => "-",
        _ => " ",
    };

    private static string[] Split(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');

        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}
