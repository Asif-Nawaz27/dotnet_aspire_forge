using AspireForge.Cli.Output;
using AspireForge.Core.Features;
using AspireForge.Core.Fixes;
using AspireForge.Generators;

namespace AspireForge.Cli.Commands;

public class FixCommand(ConsoleRenderer renderer)
{
    public static readonly IReadOnlyDictionary<string, IRuleFix> Fixes =
        RuleFixSet.CreateDefault().ToDictionary(fix => fix.RuleId, StringComparer.OrdinalIgnoreCase);

    public async Task<int> RunAsync(string ruleId)
    {
        if (!Fixes.TryGetValue(ruleId, out var fix))
        {
            renderer.WriteLine(
                $"No automatic fix is available for '{ruleId}' yet. Fixable rules: {string.Join(", ", Fixes.Keys)}.");
            return ExitCodes.InvalidConfiguration;
        }

        var root = Directory.GetCurrentDirectory();
        var solutionFile = Directory.EnumerateFiles(root, "*.sln*", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (solutionFile is null)
        {
            renderer.WriteLine($"No solution file found in '{root}'. Run this from the root of an AspireForge project.");
            return ExitCodes.InvalidConfiguration;
        }

        var context = new FeatureContext
        {
            ProjectName = Path.GetFileNameWithoutExtension(solutionFile),
            RootPath = root,
        };

        renderer.WriteLine($"Fix: {fix.RuleId}");
        renderer.WriteLine();

        try
        {
            var steps = await fix.ApplyAsync(context);

            foreach (var step in steps)
            {
                renderer.WriteCheck("✓", step);
            }

            renderer.WriteLine();
            renderer.WriteLine($"{fix.RuleId} fixed successfully.");
            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            renderer.WriteLine();
            renderer.WriteLine($"Failed to fix '{fix.RuleId}': {ex.Message}");
            return ExitCodes.GenerationFailure;
        }
    }
}
