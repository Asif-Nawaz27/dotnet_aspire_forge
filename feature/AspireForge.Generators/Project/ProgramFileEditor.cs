namespace AspireForge.Generators.Project;

internal static class ProgramFileEditor
{
    public static void AddUsingsAndServiceRegistration(
        string programCsPath, IReadOnlyList<string> usings, IReadOnlyList<string> serviceRegistrationLines)
    {
        var lines = File.ReadAllLines(programCsPath).ToList();

        InsertMissingUsings(lines, usings);
        InsertAfter(lines, "WebApplication.CreateBuilder", serviceRegistrationLines);

        File.WriteAllLines(programCsPath, lines);
    }

    public static void InsertAfterBuilderBuild(string programCsPath, IReadOnlyList<string> lines)
    {
        var fileLines = File.ReadAllLines(programCsPath).ToList();
        InsertAfter(fileLines, "builder.Build()", lines);
        File.WriteAllLines(programCsPath, fileLines);
    }

    public static void InsertBeforeAppRun(string programCsPath, IReadOnlyList<string> lines)
    {
        var fileLines = File.ReadAllLines(programCsPath).ToList();
        InsertBefore(fileLines, "app.Run(", lines);
        File.WriteAllLines(programCsPath, fileLines);
    }

    private static void InsertMissingUsings(List<string> lines, IReadOnlyList<string> usings)
    {
        var missing = usings
            .Where(ns => lines.TrueForAll(line => line.Trim() != $"using {ns};"))
            .Select(ns => $"using {ns};")
            .ToList();

        if (missing.Count > 0)
        {
            lines.InsertRange(0, missing);
        }
    }

    private static void InsertAfter(List<string> lines, string anchor, IReadOnlyList<string> newLines)
    {
        var anchorIndex = lines.FindIndex(line => line.Contains(anchor));

        if (anchorIndex < 0)
        {
            throw new InvalidOperationException($"Could not find '{anchor}' in Program.cs.");
        }

        lines.InsertRange(anchorIndex + 1, newLines.Prepend(string.Empty));
    }

    private static void InsertBefore(List<string> lines, string anchor, IReadOnlyList<string> newLines)
    {
        var anchorIndex = lines.FindIndex(line => line.Contains(anchor));

        if (anchorIndex < 0)
        {
            throw new InvalidOperationException($"Could not find '{anchor}' in Program.cs.");
        }

        lines.InsertRange(anchorIndex, newLines.Append(string.Empty));
    }
}
