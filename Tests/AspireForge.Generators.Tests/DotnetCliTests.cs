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
            ? ("cmd", new[] { "/c", "start /b ping -n 90 127.0.0.1 & echo done" })
            : ("sh", new[] { "-c", "sleep 90 & echo done" });

        var run = Task.Run(() => DotnetCli.RunProcess(
            fileName, arguments, WorkingDirectory, outputGracePeriod: TimeSpan.FromSeconds(2)));

        // Generous for a busy machine (these tests run next to real builds), and still far under the 90
        // seconds the child holds the pipes for, which the old code waited out.
        var output = await run.WaitAsync(TimeSpan.FromSeconds(45));

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

    // Regression test: `dotnet test` (or `dotnet run -- new`) sets these for its own SDK, and a child
    // `dotnet build` that inherited them built with that SDK instead of the one it would pick itself.
    // Each is set here only if this process doesn't already have it, and restored afterwards.
    [Fact]
    public void DoesNotPassOnTheParentsMSBuildPaths()
    {
        var originals = DotnetCli.InheritedMSBuildVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable);

        try
        {
            foreach (var name in DotnetCli.InheritedMSBuildVariables)
            {
                Environment.SetEnvironmentVariable(name, originals[name] ?? "inherited-from-parent");
            }

            foreach (var name in DotnetCli.InheritedMSBuildVariables)
            {
                var (fileName, arguments) = OperatingSystem.IsWindows()
                    ? ("cmd", new[] { "/c", $"if defined {name} (echo present) else (echo absent)" })
                    : ("sh", new[] { "-c", $"if [ -n \"${{{name}+x}}\" ]; then echo present; else echo absent; fi" });

                Assert.Equal("absent", DotnetCli.RunProcess(fileName, arguments, WorkingDirectory).Trim());
            }
        }
        finally
        {
            foreach (var (name, value) in originals)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
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
