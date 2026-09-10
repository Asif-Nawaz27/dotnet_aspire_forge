namespace AspireForge.Analyzers.Rules.Testing;

internal static class TestProjectLocator
{
    public static string? FindTestsDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "tests");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
