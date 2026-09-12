using System.CommandLine;
using System.Text;
using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;

Console.OutputEncoding = Encoding.UTF8;

var root = new RootCommand("AspireForge - opinionated .NET Aspire project scaffolding and analysis.");

root.Subcommands.Add(BuildNewCommand());
root.Subcommands.Add(BuildAddCommand());
root.Subcommands.Add(BuildDoctorCommand());
root.Subcommands.Add(BuildVersionCommand());
root.Subcommands.Add(BuildHelpCommand(root));

return await root.Parse(args).InvokeAsync();

static Command BuildNewCommand()
{
    var nameArgument = new Argument<string>("name")
    {
        Description = "The name of the project to generate.",
    };

    var architectureOption = new Option<string>("--architecture")
    {
        Description = "The architecture template to use.",
        DefaultValueFactory = _ => "clean",
    };

    var command = new Command("new", "Generate a new project.") { nameArgument, architectureOption };

    command.SetAction(parseResult => new NewCommand(new ConsoleRenderer()).Run(
        parseResult.GetValue(nameArgument)!,
        parseResult.GetValue(architectureOption)!));

    return command;
}

static Command BuildAddCommand()
{
    var command = new Command("add", "Add a feature to an existing AspireForge project.");

    foreach (var (id, installer) in AddCommand.FeatureInstallers)
    {
        var featureCommand = new Command(id, installer.Feature.Description);
        featureCommand.SetAction(_ => new AddCommand(new ConsoleRenderer()).RunAsync(id));
        command.Subcommands.Add(featureCommand);
    }

    return command;
}

static Command BuildDoctorCommand()
{
    var pathArgument = new Argument<string>("path")
    {
        Description = "The project file or directory to analyze.",
        DefaultValueFactory = _ => Directory.GetCurrentDirectory(),
    };

    var command = new Command("doctor", "Analyze a project against AspireForge's rule set.") { pathArgument };

    command.SetAction((parseResult, cancellationToken) => new DoctorCommand(new ConsoleRenderer())
        .RunAsync(parseResult.GetValue(pathArgument)!, cancellationToken));

    return command;
}

static Command BuildVersionCommand()
{
    var command = new Command("version", "Show the AspireForge CLI version.");
    command.SetAction(_ => new VersionCommand(new ConsoleRenderer()).Run());
    return command;
}

static Command BuildHelpCommand(RootCommand root)
{
    var command = new Command("help", "Show help and usage information.");
    command.SetAction(_ => root.Parse(["--help"]).Invoke());
    return command;
}
