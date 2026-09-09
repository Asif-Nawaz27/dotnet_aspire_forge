namespace AspireForge.Core.Generation;

public class GenerationResult
{
    public bool Success { get; init; }

    public IReadOnlyList<string> GeneratedFiles { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];
}
