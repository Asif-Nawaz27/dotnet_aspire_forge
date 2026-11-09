namespace AspireForge.Core.Configuration;

public class AspireForgeConfig
{
    public IReadOnlyDictionary<string, RuleConfig> Rules { get; init; } = new Dictionary<string, RuleConfig>();

    // Null means detect it from project names.
    public ArchitectureStyle? Architecture { get; init; }
}
