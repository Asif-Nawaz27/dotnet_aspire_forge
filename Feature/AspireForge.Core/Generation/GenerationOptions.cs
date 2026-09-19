namespace AspireForge.Core.Generation;

public class GenerationOptions
{
    public required string ProjectName { get; init; }

    public required string Architecture { get; init; }

    public required string OutputPath { get; init; }

    public bool Overwrite { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}
