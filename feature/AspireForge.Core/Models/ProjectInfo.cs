namespace AspireForge.Core.Models;

public class ProjectInfo
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public ProjectType Type { get; init; }

    public string? TargetFramework { get; init; }
}
