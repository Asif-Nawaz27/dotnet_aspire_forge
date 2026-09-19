using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;

namespace AspireForge.Cli.Tests.Commands;

public class FixCommandTests
{
    [Fact]
    public async Task RunAsync_ReturnsInvalidConfiguration_ForARuleWithNoAvailableFix()
    {
        var writer = new StringWriter();
        var command = new FixCommand(new ConsoleRenderer(writer));

        var exitCode = await command.RunAsync("ARCH001");

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("No automatic fix is available", writer.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsInvalidConfiguration_ForAnUnknownRuleId()
    {
        var command = new FixCommand(new ConsoleRenderer(new StringWriter()));

        var exitCode = await command.RunAsync("NOTAREALRULE");

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
    }
}
