using AspireForge.Cli.Output;
using AspireForge.Core.Features;
using AspireForge.Generators.Authentication;
using AspireForge.Generators.Caching;
using AspireForge.Generators.Database;
using AspireForge.Generators.Docker;
using AspireForge.Generators.Observability;

namespace AspireForge.Cli.Commands;

public class AddCommand(ConsoleRenderer renderer)
{
    private static readonly IReadOnlyDictionary<string, IFeatureInstaller> FeatureInstallers =
        new IFeatureInstaller[]
        {
            new EfCoreDatabaseFeatureInstaller(DatabaseProviders.Postgres),
            new RedisFeatureInstaller(),
            new TelemetryFeatureInstaller(),
            new DockerFeatureInstaller(),
            new AuthenticationFeatureInstaller(),
        }.ToDictionary(installer => installer.Feature.Id, StringComparer.OrdinalIgnoreCase);

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            renderer.WriteLine("Usage: aspireforge add <feature>");
            renderer.WriteLine($"Supported features: {string.Join(", ", FeatureInstallers.Keys)}");
            return 1;
        }

        var featureName = args[0];

        if (!FeatureInstallers.TryGetValue(featureName, out var installer))
        {
            renderer.WriteLine(
                $"Feature '{featureName}' is not supported yet. Supported: {string.Join(", ", FeatureInstallers.Keys)}.");
            return 1;
        }

        var root = Directory.GetCurrentDirectory();
        var solutionFile = Directory.EnumerateFiles(root, "*.sln*", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (solutionFile is null)
        {
            renderer.WriteLine($"No solution file found in '{root}'. Run this from the root of an AspireForge project.");
            return 1;
        }

        var context = new FeatureContext
        {
            ProjectName = Path.GetFileNameWithoutExtension(solutionFile),
            RootPath = root,
        };

        renderer.WriteLine($"Feature: {installer.Feature.Name}");
        renderer.WriteLine();

        try
        {
            var steps = await installer.InstallAsync(context);

            foreach (var step in steps)
            {
                renderer.WriteCheck("✓", step);
            }

            renderer.WriteLine();
            renderer.WriteLine($"{installer.Feature.Name} integration added successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            renderer.WriteLine();
            renderer.WriteLine($"Failed to add '{installer.Feature.Name}': {ex.Message}");
            return 1;
        }
    }
}
