using AspireForge.Cli.Output;
using AspireForge.Core.Generation;
using AspireForge.Generators.Project;

namespace AspireForge.Cli.Commands;

public class NewCommand(ConsoleRenderer renderer)
{
    private static readonly IReadOnlyDictionary<string, IProjectGenerator> Generators =
        new IProjectGenerator[] { new CleanArchitectureGenerator() }
            .ToDictionary(generator => generator.Name, StringComparer.OrdinalIgnoreCase);

    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            renderer.WriteLine("Usage: aspireforge new <name> [--architecture <name>]");
            return 1;
        }

        var projectName = args[0];

        if (string.IsNullOrWhiteSpace(projectName) || projectName.Any(Path.GetInvalidFileNameChars().Contains))
        {
            renderer.WriteLine($"'{projectName}' is not a valid project name.");
            return 1;
        }

        var architecture = "clean";

        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--architecture" && i + 1 < args.Length)
            {
                architecture = args[++i];
            }
        }

        if (!Generators.TryGetValue(architecture, out var generator))
        {
            renderer.WriteLine(
                $"Architecture '{architecture}' is not supported yet. Supported: {string.Join(", ", Generators.Keys)}.");
            return 1;
        }

        var options = new GenerationOptions
        {
            ProjectName = projectName,
            Architecture = architecture,
            OutputPath = Directory.GetCurrentDirectory(),
        };

        var result = generator.Generate(options);

        if (!result.Success)
        {
            renderer.WriteLine($"Failed to generate '{projectName}':");
            foreach (var error in result.Errors)
            {
                renderer.WriteLine($"  {error}");
            }

            return 1;
        }

        renderer.WriteLine($"Created {projectName} ({architecture} architecture):");
        foreach (var file in result.GeneratedFiles)
        {
            renderer.WriteLine($"  {file}");
        }

        return 0;
    }
}
