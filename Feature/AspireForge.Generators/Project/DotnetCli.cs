using System.Diagnostics;
using System.Text;

namespace AspireForge.Generators.Project;

public static class DotnetCli
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    // How long to keep reading output after the process has exited. Normally the pipes close the moment
    // it exits; they only stay open when something it started is still holding them (see RunProcess).
    private static readonly TimeSpan DefaultOutputGracePeriod = TimeSpan.FromSeconds(10);

    public static void Run(IReadOnlyList<string> arguments, string workingDirectory, TimeSpan? timeout = null) =>
        RunForOutput(arguments, workingDirectory, timeout);

    // Standard output, for commands whose result is needed (`dotnet package search --format json`).
    public static string RunForOutput(IReadOnlyList<string> arguments, string workingDirectory, TimeSpan? timeout = null) =>
        RunProcess("dotnet", arguments, workingDirectory, timeout);

    // Waits for the process itself, not for its output pipes to close. The two used to be the same wait,
    // and it hung forever: `dotnet build`, `dotnet add package` and other MSBuild-driven commands can leave
    // an MSBuild node or compiler server running after they exit, and that process inherits the
    // redirected stdout/stderr, so reading to the end never finishes. The process timeout didn't help,
    // because the process had already exited.
    //
    // So: output is read as it arrives, the process gets its timeout, and the pipes get a short grace
    // period after it exits - enough for the last of its output, not for a process left behind. Node reuse
    // is also turned off, so the commands this runs don't leave MSBuild nodes behind in the first place.
    internal static string RunProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan? timeout = null,
        TimeSpan? outputGracePeriod = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        var commandLine = $"{fileName} {string.Join(' ', arguments)}";
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var process = new Process { StartInfo = startInfo };

        // Both streams are read concurrently, as they arrive: a child that fills one pipe's buffer while
        // nothing reads it would block forever. A null line means that stream has closed.
        process.OutputDataReceived += (_, e) => Append(stdout, stdoutClosed, e.Data);
        process.ErrorDataReceived += (_, e) => Append(stderr, stderrClosed, e.Data);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start '{commandLine}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeoutMilliseconds = (int)(timeout ?? DefaultTimeout).TotalMilliseconds;

        // The timed overload returns when the process exits, without waiting for the pipes to close.
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            TryKillProcessTree(process);
            throw new TimeoutException($"'{commandLine}' did not exit within {timeoutMilliseconds}ms and was terminated.");
        }

        Task.WaitAll([stdoutClosed.Task, stderrClosed.Task], outputGracePeriod ?? DefaultOutputGracePeriod);

        string output, errors;
        lock (stdout)
        {
            output = stdout.ToString();
        }

        lock (stderr)
        {
            errors = stderr.ToString();
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{commandLine}' failed with exit code {process.ExitCode}.{Environment.NewLine}{errors}{output}");
        }

        return output;
    }

    private static void Append(StringBuilder buffer, TaskCompletionSource closed, string? line)
    {
        if (line is null)
        {
            closed.TrySetResult();
            return;
        }

        lock (buffer)
        {
            buffer.AppendLine(line);
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort - the process may have already exited on its own.
        }
    }
}
