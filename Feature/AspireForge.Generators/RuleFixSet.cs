using AspireForge.Core.Fixes;
using AspireForge.Generators.Fixes;

namespace AspireForge.Generators;

// Only rules with a safe, unambiguous automatic fix are registered here. Most of the 10 rules
// don't have one yet (ARCH001 is a refactor, TEST001/TEST002 need a whole new project, SEC001/
// SEC002/OBS001/OBS002 already have a richer fix via `add` and don't need a second path) - this
// grows the same way AnalysisRuleSet's rule list and AddCommand's feature registry did, one entry
// at a time.
public static class RuleFixSet
{
    public static IReadOnlyList<IRuleFix> CreateDefault() =>
    [
        new REL001HealthChecksFix(),
        new SEC003HttpsRedirectionFix(),
    ];
}
