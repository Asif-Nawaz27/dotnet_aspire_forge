using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests;

internal sealed class TestProject : IDisposable
{
    private readonly string _directory;

    private TestProject(string directory, ProjectContext context)
    {
        _directory = directory;
        Context = context;
    }

    public ProjectContext Context { get; }

    public static TestProject Create(
        string? programCsContent = null,
        IReadOnlyCollection<string>? packageReferences = null,
        IReadOnlyCollection<string>? projectReferences = null,
        string projectName = "SampleApi")
    {
        var directory = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var sourceFiles = new List<string>();

        if (programCsContent is not null)
        {
            var programPath = Path.Combine(directory, "Program.cs");
            File.WriteAllText(programPath, programCsContent);
            sourceFiles.Add(programPath);
        }

        var context = new ProjectContext
        {
            RootDirectory = directory,
            ProjectFile = Path.Combine(directory, $"{projectName}.csproj"),
            ProjectName = projectName,
            SourceFiles = sourceFiles,
            ProjectReferences = projectReferences ?? [],
            PackageReferences = packageReferences ?? [],
        };

        return new TestProject(directory, context);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
