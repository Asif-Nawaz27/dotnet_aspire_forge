namespace AspireForge.Generators.Project;

internal static class ProgramFileEditor
{
    public static void AddUsingsAndServiceRegistration(
        string programCsPath, IReadOnlyList<string> usings, IReadOnlyList<string> serviceRegistrationLines)
    {
        var lines = File.ReadAllLines(programCsPath).ToList();

        InsertMissingUsings(lines, usings);
        InsertAfterBuilderCreation(lines, serviceRegistrationLines);

        File.WriteAllLines(programCsPath, lines);
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

    private static void InsertAfterBuilderCreation(List<string> lines, IReadOnlyList<string> serviceRegistrationLines)
    {
        var anchorIndex = lines.FindIndex(line => line.Contains("WebApplication.CreateBuilder"));

        if (anchorIndex < 0)
        {
            throw new InvalidOperationException("Could not find 'WebApplication.CreateBuilder' in Program.cs.");
        }

        lines.InsertRange(anchorIndex + 1, serviceRegistrationLines.Prepend(string.Empty));
    }
}
