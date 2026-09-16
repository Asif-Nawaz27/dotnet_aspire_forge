using AspireForge.Cli.Commands;
using AspireForge.Cli.Output;

namespace AspireForge.Cli.Tests.Commands;

public class UpdateCommandTests
{
    [Fact]
    public async Task RunAsync_ReturnsInvalidConfiguration_WhenNoSolutionFileIsFound()
    {
        var previousDirectory = Directory.GetCurrentDirectory();
        var emptyDirectory = Path.Combine(Path.GetTempPath(), "aspireforge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDirectory);

        try
        {
            Directory.SetCurrentDirectory(emptyDirectory);

            var writer = new StringWriter();
            var command = new UpdateCommand(new ConsoleRenderer(writer));

            var exitCode = await command.RunAsync();

            Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
            Assert.Contains("No solution file found", writer.ToString());
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Directory.Delete(emptyDirectory, recursive: true);
        }
    }
}
