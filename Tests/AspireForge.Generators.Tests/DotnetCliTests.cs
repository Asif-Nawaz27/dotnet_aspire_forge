using AspireForge.Generators.Project;

namespace AspireForge.Generators.Tests;

public class DotnetCliTests
{
    private static readonly string WorkingDirectory = Path.GetTempPath();

    // Regression test: the command exits at once, but leaves a child running that inherited its
    // stdout/stderr, as a reused MSBuild node or compiler server does after `dotnet build`. Reading the
    // output to the end used to wait for that child, which in the integration tests meant forever.
    [Fact]
    public async Task ReturnsOnceTheProcessExits_EvenWhenAChildItStartedStillHoldsTheOutput()
    {
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("cmd", new[] { "/c", "start /b ping -n 30 127.0.0.1 & echo done" })
            : ("sh", new[] { "-c", "sleep 30 & echo done" });

        var run = Task.Run(() => DotnetCli.RunProcess(
            fileName, arguments, WorkingDirectory, outputGracePeriod: TimeSpan.FromSeconds(1)));

        // Well under the 30 seconds the child holds the pipes for.
        var output = await run.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Contains("done", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnsAllOfStandardOutput()
    {
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("cmd", new[] { "/c", "echo one& echo two& echo three" })
            : ("sh", new[] { "-c", "echo one; echo two; echo three" });

        var lines = DotnetCli.RunProcess(fileName, arguments, WorkingDirectory)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(["one", "two", "three"], lines);
    }

    // The message also contains the command line, so the expected text is computed by the process
    // (an environment variable, arithmetic) and only appears in what it wrote to stderr.
    [Fact]
    public void ReportsAFailure_WithItsErrorOutput()
    {
        var (fileName, arguments, expected) = OperatingSystem.IsWindows()
            ? ("cmd", new[] { "/c", "echo %OS%-stderr 1>&2& exit 3" }, "Windows_NT-stderr")
            : ("sh", new[] { "-c", "echo $((40 + 2))-stderr >&2; exit 3" }, "42-stderr");

        var error = Assert.Throws<InvalidOperationException>(() => DotnetCli.RunProcess(fileName, arguments, WorkingDirectory));

        Assert.Contains("exit code 3", error.Message, StringComparison.Ordinal);
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StopsAProcessThatRunsPastItsTimeout()
    {
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("cmd", new[] { "/c", "ping -n 30 127.0.0.1" })
            : ("sh", new[] { "-c", "sleep 30" });

        Assert.Throws<TimeoutException>(() =>
            DotnetCli.RunProcess(fileName, arguments, WorkingDirectory, timeout: TimeSpan.FromSeconds(1)));
    }

    // The MSBuild-driven commands this runs mustn't leave nodes behind to hold the pipes in the first place.
    [Fact]
    public void TurnsOffMSBuildNodeReuse_ForTheCommandsItRuns()
    {
        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("cmd", new[] { "/c", "echo %MSBUILDDISABLENODEREUSE%" })
            : ("sh", new[] { "-c", "echo $MSBUILDDISABLENODEREUSE" });

        Assert.Equal("1", DotnetCli.RunProcess(fileName, arguments, WorkingDirectory).Trim());
    }
}
