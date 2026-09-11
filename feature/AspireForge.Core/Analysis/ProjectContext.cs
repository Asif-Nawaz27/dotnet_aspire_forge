namespace AspireForge.Core.Analysis;

public sealed class ProjectContext
{
    public required string RootDirectory { get; init; }

    public required string ProjectFile { get; init; }

    public required string ProjectName { get; init; }

    public string? TargetFramework { get; init; }

    public required IReadOnlyCollection<string> SourceFiles { get; init; }

    public required IReadOnlyCollection<string> ProjectReferences { get; init; }

    public required IReadOnlyCollection<string> PackageReferences { get; init; }
}
