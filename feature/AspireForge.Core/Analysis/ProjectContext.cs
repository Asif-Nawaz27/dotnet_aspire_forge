using AspireForge.Core.Models;

namespace AspireForge.Core.Analysis;

public class ProjectContext
{
    public required ProjectInfo Project { get; init; }

    public required string RootPath { get; init; }
}
