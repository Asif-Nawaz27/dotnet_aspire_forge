using AspireForge.Cli.Output;
using AspireForge.Core.Features;
using AspireForge.Generators;

namespace AspireForge.Cli.Commands;

public class UpdateCommand(ConsoleRenderer renderer)
{
    private static readonly IReadOnlyList<IFeatureInstaller> Installers = UpdateFeatureSet.CreateDefault();

    public async Task<int> RunAsync()
    {
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

        renderer.WriteLine($"Updating {context.ProjectName}");
        renderer.WriteLine();

        try
        {
            foreach (var installer in Installers)
            {
                var steps = await installer.InstallAsync(context);

                foreach (var step in steps)
                {
                    renderer.WriteCheck("✓", step);
                }
            }

            renderer.WriteLine();
            renderer.WriteLine("Update complete.");
            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            renderer.WriteLine();
            renderer.WriteLine($"Update failed: {ex.Message}");
            return ExitCodes.GenerationFailure;
        }
    }
}
