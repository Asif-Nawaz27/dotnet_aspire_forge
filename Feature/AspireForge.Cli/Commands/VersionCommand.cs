using System.Reflection;
using AspireForge.Cli.Output;

namespace AspireForge.Cli.Commands;

public class VersionCommand(ConsoleRenderer renderer)
{
    public void Run()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        renderer.WriteLine($"aspireforge {version}");
    }
}
