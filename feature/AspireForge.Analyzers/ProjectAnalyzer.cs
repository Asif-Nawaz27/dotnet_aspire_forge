using System.Xml.Linq;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers;

public class ProjectAnalyzer(IEnumerable<IAnalysisRule> rules) : IProjectAnalyzer
{
    private readonly IReadOnlyList<IAnalysisRule> _rules = rules.ToList();

    public async Task<AnalysisResult> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var projectFile = LoadProject(path);
        var context = BuildProjectContext(projectFile);
        var rules = LoadRules();
        var issues = await ExecuteRules(rules, context, cancellationToken);

        return ReturnAnalysisResult(context, issues);
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

    private static ProjectContext BuildProjectContext(string projectFile)
    {
        var rootDirectory = Path.GetDirectoryName(projectFile)
            ?? throw new InvalidOperationException($"Could not determine directory for '{projectFile}'.");

        var document = XDocument.Load(projectFile);

        var projectReferences = ReadIncludes(document, "ProjectReference");
        var packageReferences = ReadIncludes(document, "PackageReference");

        var sourceFiles = Directory
            .EnumerateFiles(rootDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsInBuildOutputDirectory(file))
            .ToList();

        return new ProjectContext
        {
            RootDirectory = rootDirectory,
            ProjectFile = projectFile,
            ProjectName = Path.GetFileNameWithoutExtension(projectFile),
            SourceFiles = sourceFiles,
            ProjectReferences = projectReferences,
            PackageReferences = packageReferences,
        };
    }

    private IReadOnlyList<IAnalysisRule> LoadRules() => _rules;

    private static async Task<IReadOnlyCollection<AnalysisIssue>> ExecuteRules(
        IReadOnlyList<IAnalysisRule> rules,
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(
            rules.Select(rule => rule.EvaluateAsync(context, cancellationToken)));

        return results.Where(issue => issue is not null).Select(issue => issue!).ToList();
    }

    private static AnalysisResult ReturnAnalysisResult(
        ProjectContext context,
        IReadOnlyCollection<AnalysisIssue> issues) =>
        new(context.ProjectName, issues);

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
