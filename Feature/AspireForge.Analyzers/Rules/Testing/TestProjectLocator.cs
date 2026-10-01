namespace AspireForge.Analyzers.Rules.Testing;

internal static class TestProjectLocator
{
    private const string TestsDirectoryName = "tests";

    // Matches the folder name case-insensitively and returns it with its real on-disk casing. A plain
    // Directory.Exists(Path.Combine(dir, "tests")) only works on case-insensitive file systems: on
    // Linux/macOS a "Tests/" folder is never found, and TEST001/TEST002 silently pass instead of checking.
    public static string? FindTestsDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            var candidate = FindChildDirectory(current, TestsDirectoryName);

            if (candidate is not null)
            {
                return candidate.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static DirectoryInfo? FindChildDirectory(DirectoryInfo parent, string name)
    {
        try
        {
            return parent
                .EnumerateDirectories()
                .FirstOrDefault(directory => string.Equals(directory.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Walking upward can reach directories this process can't list (e.g. above a user's home
            // directory) - treat those as having no tests folder rather than failing the whole analysis.
            return null;
        }
    }
}
