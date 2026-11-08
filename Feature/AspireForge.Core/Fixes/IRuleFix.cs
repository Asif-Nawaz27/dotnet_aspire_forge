namespace AspireForge.Core.Fixes;

// Deliberately separate from IAnalysisRule: a rule knows how to detect a problem, not how to fix
// it, the same way a linter's diagnostic and its code-fix provider are independent. Not every rule
// has (or needs) a fix - RuleFixSet only registers the ones that do.
public interface IRuleFix
{
    string RuleId { get; }

    string Description { get; }

    // Makes its edits in the workspace and returns what it did. Writing to disk directly would break
    // --dry-run and leave a project half-fixed when a later fix fails.
    Task<IReadOnlyList<string>> ApplyAsync(FixWorkspace workspace, CancellationToken cancellationToken = default);
}
