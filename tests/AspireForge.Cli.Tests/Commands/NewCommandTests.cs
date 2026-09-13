using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;

namespace AspireForge.Cli.Tests.Commands;

public class NewCommandTests
{
    [Fact]
    public void Run_ReturnsInvalidConfiguration_ForABlankProjectName()
    {
        var command = new NewCommand(new ConsoleRenderer(new StringWriter()));

        var exitCode = command.Run(projectName: "   ", architecture: "clean");

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
    }

    [Fact]
    public void Run_ReturnsInvalidConfiguration_ForAnUnsupportedArchitecture()
    {
        var writer = new StringWriter();
        var command = new NewCommand(new ConsoleRenderer(writer));

        var exitCode = command.Run(projectName: "SampleApi", architecture: "vertical-slice");

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("not supported yet", writer.ToString());
    }
}
