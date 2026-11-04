namespace AspireForge.Cli.Output;

// The one command that resolves a rule's finding, when one exists:
//   - `aspireforge fix <rule>` - an automatic fix for that rule (RuleFixSet)
//   - `aspireforge add <feature>` - a feature that, once added, satisfies the rule
// Rules missing from both need a code change by hand; their docs explain how.
public static class RuleRemedies
{
    // Rules an `add` feature satisfies - kept to the cases where adding the feature really does make
    // the finding go away (see each feature's docs under docs/features/).
    private static readonly Dictionary<string, string> AddFeatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SEC001"] = "authentication",
        ["OBS001"] = "telemetry",
        ["OBS002"] = "telemetry",
    };

    public static string? CommandFor(string ruleId, IReadOnlySet<string> automaticallyFixableRuleIds) =>
        automaticallyFixableRuleIds.Contains(ruleId)
            ? $"aspireforge fix {ruleId}"
            : AddFeatures.TryGetValue(ruleId, out var feature)
                ? $"aspireforge add {feature}"
                : null;
}
