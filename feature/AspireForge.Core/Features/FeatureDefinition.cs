namespace AspireForge.Core.Features;

public class FeatureDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }
}
