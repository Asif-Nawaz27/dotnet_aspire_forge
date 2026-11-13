using AspireForge.Analyzers;
using AspireForge.Analyzers.Configuration;
using AspireForge.Analyzers.Suppressions;
using AspireForge.Cli.Output;
using AspireForge.Core.Analysis;
using AspireForge.Core.Features;
using AspireForge.Core.Fixes;
using AspireForge.Generators;

namespace AspireForge.Cli.Commands;

public class FixCommand(ConsoleRenderer renderer)
{
    public static readonly IReadOnlyDictionary<string, IRuleFix> Fixes =
        RuleFixSet.CreateDefault().ToDictionary(fix => fix.RuleId, StringComparer.OrdinalIgnoreCase);

    // Which fixes run is decided by what doctor reports rather than what was asked for. A fix adds code,
    // so running one for a rule that already passes would add that code a second time.
    public async Task<int> RunAsync(
        string? ruleId,
        bool all = false,
        bool dryRun = false,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (all == (ruleId is not null))
        {
            renderer.WriteLine(all
                ? "Give a rule ID or --all, not both."
                : $"Give a rule ID or --all. Fixable rules: {string.Join(", ", Fixes.Keys)}.");
            return ExitCodes.InvalidConfiguration;
        }

        if (ruleId is not null && !Fixes.ContainsKey(ruleId))
        {
            renderer.WriteLine(
                $"No automatic fix is available for '{ruleId}' yet. Fixable rules: {string.Join(", ", Fixes.Keys)}.");
            return ExitCodes.InvalidConfiguration;
        }

        var root = workingDirectory ?? Directory.GetCurrentDirectory();
        var solutionFile = Directory.EnumerateFiles(root, "*.sln*", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (solutionFile is null)
        {
            renderer.WriteLine($"No solution file found in '{root}'. Run this from the root of an AspireForge project.");
            return ExitCodes.InvalidConfiguration;
        }

        IReadOnlyList<IRuleFix> fixes;

        try
        {
            fixes = all
                ? await FindFixesForReportedFindingsAsync(root, cancellationToken)
                : await FindFixForRuleAsync(root, Fixes[ruleId!], cancellationToken);
        }
        catch (FileNotFoundException)
        {
            renderer.WriteLine($"No Api project found in '{root}'. Run this from the root of an AspireForge project.");
            return ExitCodes.InvalidConfiguration;
        }
        catch (InvalidDataException exception)
        {
            renderer.WriteLine(exception.Message);
            return ExitCodes.InvalidConfiguration;
        }

        if (fixes.Count == 0)
        {
            renderer.WriteLine(all
                ? "Nothing to fix: doctor reports no finding with an automatic fix."
                : $"Nothing to fix: doctor doesn't report {ruleId!.ToUpperInvariant()}.");
            return ExitCodes.Success;
        }

        var workspace = new FixWorkspace(new FeatureContext
        {
            ProjectName = Path.GetFileNameWithoutExtension(solutionFile),
            RootPath = root,
        });

        var steps = new List<(IRuleFix Fix, IReadOnlyList<string> Steps)>();

        foreach (var fix in fixes)
        {
            try
            {
                steps.Add((fix, await fix.ApplyAsync(workspace, cancellationToken)));
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                renderer.WriteLine($"Failed to fix '{fix.RuleId}': {exception.Message}");
                renderer.WriteLine("Nothing was changed.");
                return ExitCodes.GenerationFailure;
            }
        }

        // Like `dotnet format --verify-no-changes`: 1 when there's something to fix, so CI can fail on it.
        if (dryRun)
        {
            WritePreview(workspace, steps.Select(step => step.Fix).ToList(), root);
            return workspace.Changes.Count > 0 ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;
        }

        workspace.Commit();
        WriteApplied(steps);

        return ExitCodes.Success;
    }

    // A named rule is an explicit request, so the baseline and config don't get a say: someone running
    // `fix SEC003` on a baselined finding is choosing to fix it. It only has to really be there.
    private static async Task<IReadOnlyList<IRuleFix>> FindFixForRuleAsync(
        string root, IRuleFix fix, CancellationToken cancellationToken)
    {
        var rule = AnalysisRuleSet.CreateDefault().Single(rule => rule.Id == fix.RuleId);
        var result = await new ProjectAnalyzer([rule]).AnalyzeAsync(root, cancellationToken);

        return result.Issues.Count > 0 ? [fix] : [];
    }

    // --all follows doctor exactly: what's been disabled, suppressed, or baselined has been accepted, and
    // a bulk fix shouldn't overrule that.
    private static async Task<IReadOnlyList<IRuleFix>> FindFixesForReportedFindingsAsync(
        string root, CancellationToken cancellationToken)
    {
        var context = ProjectContextBuilder.Build(root);
        var config = ConfigLoader.Load(context.RootDirectory);
        var rules = AnalysisRuleSet.ApplyConfig(AnalysisRuleSet.CreateDefault(), config)
            .Where(rule => Fixes.ContainsKey(rule.Id))
            .ToList();
        var suppressions = FindingSuppressions.Create(context.RootDirectory, BaselineFile.Load(context.RootDirectory));
        var result = await new ProjectAnalyzer(rules, suppressions).AnalyzeAsync(root, cancellationToken);
        var reported = result.Issues.Select(issue => issue.RuleId).ToHashSet();

        return RuleFixSet.CreateDefault().Where(fix => reported.Contains(fix.RuleId)).ToList();
    }

    private void WritePreview(FixWorkspace workspace, IReadOnlyList<IRuleFix> fixes, string root)
    {
        renderer.WriteLine("Dry run: nothing was changed.");
        renderer.WriteLine();

        foreach (var fix in fixes)
        {
            renderer.WriteLine($"  {fix.RuleId.PadRight(8)} {fix.Description}");
        }

        foreach (var change in workspace.Changes)
        {
            renderer.WriteLine();

            var path = Path.GetRelativePath(root, change.Path).Replace('\\', '/');

            foreach (var line in UnifiedDiff.Build(path, change.Before, change.After))
            {
                renderer.WriteLine(line);
            }
        }

        var count = workspace.Changes.Count;

        renderer.WriteLine();
        renderer.WriteLine($"{count} file{(count == 1 ? string.Empty : "s")} would change. Run without --dry-run to apply.");
    }

    private void WriteApplied(IReadOnlyList<(IRuleFix Fix, IReadOnlyList<string> Steps)> applied)
    {
        foreach (var (fix, steps) in applied)
        {
            renderer.WriteLine($"Fix: {fix.RuleId}");
            renderer.WriteLine();

            foreach (var step in steps)
            {
                renderer.WriteCheck("✓", step);
            }

            renderer.WriteLine();
            renderer.WriteLine($"{fix.RuleId} fixed successfully.");
            renderer.WriteLine();
        }
    }
}
