namespace AspireForge.Generators.Project;

internal static class ProgramFileEditor
{
    public static void AddUsingsAndServiceRegistration(
        string programCsPath, IReadOnlyList<string> usings, IReadOnlyList<string> serviceRegistrationLines)
    {
        var lines = File.ReadAllLines(programCsPath).ToList();

        AddUsingsAndServiceRegistration(lines, usings, serviceRegistrationLines);

        File.WriteAllLines(programCsPath, lines);
    }

    public static void AddUsingsAndServiceRegistration(
        List<string> lines, IReadOnlyList<string> usings, IReadOnlyList<string> serviceRegistrationLines)
    {
        InsertMissingUsings(lines, usings);
        InsertAfter(lines, "WebApplication.CreateBuilder", serviceRegistrationLines);
    }

    public static void InsertAfterBuilderBuild(string programCsPath, IReadOnlyList<string> lines)
    {
        var fileLines = File.ReadAllLines(programCsPath).ToList();
        InsertAfterBuilderBuild(fileLines, lines);
        File.WriteAllLines(programCsPath, fileLines);
    }

    public static void InsertAfterBuilderBuild(List<string> fileLines, IReadOnlyList<string> lines) =>
        InsertAfter(fileLines, "builder.Build()", lines);

    // Middleware that must run before a later middleware (e.g. authentication before the rate limiter, as
    // SEC009 expects) goes just before `beforeAnchor` when it's present; otherwise - a project that never
    // added that middleware - just after builder.Build(), as before.
    public static void InsertBeforeOrAfterBuilderBuild(string programCsPath, string beforeAnchor, IReadOnlyList<string> lines)
    {
        var fileLines = File.ReadAllLines(programCsPath).ToList();

        if (fileLines.Exists(line => line.Contains(beforeAnchor)))
        {
            InsertBefore(fileLines, beforeAnchor, lines);
        }
        else
        {
            InsertAfter(fileLines, "builder.Build()", lines);
        }

        File.WriteAllLines(programCsPath, fileLines);
    }

    public static void InsertBeforeAppRun(string programCsPath, IReadOnlyList<string> lines)
    {
        var fileLines = File.ReadAllLines(programCsPath).ToList();
        InsertBeforeAppRun(fileLines, lines);
        File.WriteAllLines(programCsPath, fileLines);
    }

    public static void InsertBeforeAppRun(List<string> fileLines, IReadOnlyList<string> lines) =>
        InsertBefore(fileLines, "app.Run(", lines);

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
