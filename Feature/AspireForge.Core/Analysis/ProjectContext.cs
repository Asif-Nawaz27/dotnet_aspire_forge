namespace AspireForge.Core.Analysis;

public sealed record ProjectContext
{
    public required string RootDirectory { get; init; }

    public required string ProjectFile { get; init; }

    public required string ProjectName { get; init; }

    // The first of TargetFrameworks - kept for display and existing callers.
    public string? TargetFramework { get; init; }

    // Every framework the project builds for: one entry for <TargetFramework>, several for a
    // multi-targeted <TargetFrameworks>. Falls back to the nearest Directory.Build.props when the
    // project file sets neither. Empty when it can't be determined (e.g. an MSBuild property reference).
    public IReadOnlyCollection<string> TargetFrameworks { get; init; } = [];

    public required IReadOnlyCollection<string> SourceFiles { get; init; }

    public required IReadOnlyCollection<string> ProjectReferences { get; init; }

    public required IReadOnlyCollection<string> PackageReferences { get; init; }

    // Rules ask this about each occurrence they find - see IFindingSuppressions.
    public IFindingSuppressions Suppressions { get; init; } = NoFindingSuppressions.Instance;

    // The architecture set in config, for the architecture rules. Null means detect it.
    public Configuration.ArchitectureStyle? Architecture { get; init; }
}
