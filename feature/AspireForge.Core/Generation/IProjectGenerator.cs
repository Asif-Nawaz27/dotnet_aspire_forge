namespace AspireForge.Core.Generation;

public interface IProjectGenerator
{
    string Name { get; }

    GenerationResult Generate(GenerationOptions options);
}
