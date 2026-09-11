using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;

if (args.Length == 0)
{
    Console.WriteLine("Usage: aspireforge <command> [path]");
    return 1;
}

switch (args[0])
{
    case "doctor":
        var path = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
        await new DoctorCommand(new ConsoleRenderer()).RunAsync(path);
        return 0;

    case "new":
        return new NewCommand(new ConsoleRenderer()).Run(args.Skip(1).ToArray());

    default:
        Console.WriteLine($"Unknown command: {args[0]}");
        return 1;
}
