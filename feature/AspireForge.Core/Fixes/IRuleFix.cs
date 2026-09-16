using AspireForge.Core.Features;

namespace AspireForge.Core.Fixes;

// Deliberately separate from IAnalysisRule: a rule knows how to detect a problem, not how to fix
// it, the same way a linter's diagnostic and its code-fix provider are independent. Not every rule
// has (or needs) a fix - RuleFixSet only registers the ones that do.
public interface IRuleFix
{
    string RuleId { get; }

    string Description { get; }

    Task<IReadOnlyList<string>> ApplyAsync(FeatureContext context, CancellationToken cancellationToken = default);
}
