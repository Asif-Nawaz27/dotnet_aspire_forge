namespace AspireForge.Core.Configuration;

public class AspireForgeConfig
{
    public IReadOnlyDictionary<string, RuleConfig> Rules { get; init; } = new Dictionary<string, RuleConfig>();
}
