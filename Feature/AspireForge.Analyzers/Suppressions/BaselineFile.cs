using System.Text.Json;

namespace AspireForge.Analyzers.Suppressions;

// One accepted group of identical occurrences. The fingerprint identifies the line's content (or, for a
// finding with no line, its description) rather than its number, so an entry still matches after code
// moves. Count is how many identical occurrences are accepted in this file - one more is a new finding.
public sealed record BaselineEntry(string RuleId, string File, string Fingerprint, int Count);

// .aspireforge/baseline.json in the project directory: the findings a team has accepted for now, so
// doctor can fail on new ones without first fixing everything. Unlike config.json it isn't searched for
// upward - its paths are relative to one project, and sharing a file would let one project's update
// wipe another's entries.
public sealed record BaselineFile(int Version, IReadOnlyList<BaselineEntry> Findings)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string PathFor(string projectDirectory) =>
        Path.Combine(projectDirectory, ".aspireforge", "baseline.json");

    // Null when the project has no baseline. Throws InvalidDataException when the file can't be used:
    // ignoring it would report every accepted finding as new, with no hint why.
    public static BaselineFile? Load(string projectDirectory)
    {
        var path = PathFor(projectDirectory);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var baseline = JsonSerializer.Deserialize<BaselineFile>(File.ReadAllText(path), JsonOptions);

            return baseline?.Findings is not null
                ? baseline
                : throw new InvalidDataException($"The baseline at '{path}' has no findings list.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The baseline at '{path}' isn't valid JSON: {exception.Message}", exception);
        }
    }

    // Sorted, so regenerating an unchanged baseline leaves the file identical and diffs stay readable.
    public void Save(string projectDirectory)
    {
        var path = PathFor(projectDirectory);
        var sorted = this with
        {
            Findings = Findings
                .OrderBy(entry => entry.RuleId, StringComparer.Ordinal)
                .ThenBy(entry => entry.File, StringComparer.Ordinal)
                .ThenBy(entry => entry.Fingerprint, StringComparer.Ordinal)
                .ToList(),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(sorted, JsonOptions) + Environment.NewLine);
    }
}
