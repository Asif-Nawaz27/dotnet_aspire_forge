using AspireForge.Cli.Output;

namespace AspireForge.Cli.Commands;

public class RulesCommand(ConsoleRenderer renderer)
{
    public int List()
    {
        var isFirstGroup = true;

        foreach (var category in RuleCatalog.Entries.Select(entry => entry.Category).Distinct())
        {
            if (!isFirstGroup)
            {
                renderer.WriteLine();
            }

            isFirstGroup = false;

            foreach (var entry in RuleCatalog.Entries.Where(e => e.Category == category))
            {
                renderer.WriteLine($"{entry.RuleId} {entry.ShortName}");
            }
        }

        return ExitCodes.Success;
    }
}
