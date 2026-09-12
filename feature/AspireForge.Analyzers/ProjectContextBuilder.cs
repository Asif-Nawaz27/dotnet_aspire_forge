using System.Xml.Linq;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers;

public static class ProjectContextBuilder
{
    public static ProjectContext Build(string path)
    {
        var projectFile = LoadProject(path);
        var rootDirectory = Path.GetDirectoryName(projectFile)
            ?? throw new InvalidOperationException($"Could not determine directory for '{projectFile}'.");

        var document = XDocument.Load(projectFile);
        var projectReferences = ReadIncludes(document, "ProjectReference");

        // A referenced project's code (e.g. a shared ServiceDefaults project configuring telemetry or
        // health checks) effectively runs as part of this service too, so rules that look for patterns
        // in source text should see it. Package references stay project-local (e.g. ARCH001 needs to
        // know what THIS project directly depends on).
        var sourceFiles = EnumerateSourceFiles(rootDirectory)
            .Concat(projectReferences.SelectMany(reference => EnumerateReferencedSourceFiles(rootDirectory, reference)))
            .Distinct()
            .ToList();

        return new ProjectContext
        {
            RootDirectory = rootDirectory,
            ProjectFile = projectFile,
            ProjectName = Path.GetFileNameWithoutExtension(projectFile),
            TargetFramework = ReadTargetFramework(document),
            SourceFiles = sourceFiles,
            ProjectReferences = projectReferences,
            PackageReferences = ReadIncludes(document, "PackageReference"),
        };
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsInBuildOutputDirectory(file));

    private static IEnumerable<string> EnumerateReferencedSourceFiles(string rootDirectory, string referenceRelativePath)
    {
        var referencedProjectFile = Path.GetFullPath(Path.Combine(rootDirectory, referenceRelativePath));
        var referencedDirectory = Path.GetDirectoryName(referencedProjectFile);

        return referencedDirectory is not null && Directory.Exists(referencedDirectory)
            ? EnumerateSourceFiles(referencedDirectory)
            : [];
    }

    private static string LoadProject(string path)
    {
        var projectFile = Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault()
            : path;

        if (projectFile is null || !File.Exists(projectFile))
        {
            throw new FileNotFoundException($"No project file found at '{path}'.", path);
        }

        return Path.GetFullPath(projectFile);
    }

    private static string? ReadTargetFramework(XDocument document)
    {
        var targetFramework = document.Descendants("TargetFramework").FirstOrDefault()?.Value;
        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            return targetFramework;
        }

        var targetFrameworks = document.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
        return targetFrameworks?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
    }

    private static List<string> ReadIncludes(XDocument document, string elementName) =>
        document
            .Descendants(elementName)
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();

    private static bool IsInBuildOutputDirectory(string filePath) =>
        filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin");
}
