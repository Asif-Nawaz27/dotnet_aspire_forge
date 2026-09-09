using AspireForge.Core.Models;

namespace AspireForge.Core.Features;

public class FeatureContext
{
    public required ProjectInfo Project { get; init; }

    public required string RootPath { get; init; }
}
