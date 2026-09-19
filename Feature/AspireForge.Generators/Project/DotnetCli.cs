using System.Diagnostics;

namespace AspireForge.Generators.Project;

public static class DotnetCli
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public static void Run(IReadOnlyList<string> arguments, string workingDirectory, TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
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

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start 'dotnet {string.Join(' ', arguments)}'.");

        // Read both streams concurrently, not sequentially: a child that writes enough to stderr
        // while we're still blocked draining stdout (or vice versa) deadlocks once its pipe buffer
        // fills, since it can't proceed until we read it and we won't get there until it exits.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        var timeoutMilliseconds = (int)(timeout ?? DefaultTimeout).TotalMilliseconds;

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            TryKillProcessTree(process);
            throw new TimeoutException(
                $"'dotnet {string.Join(' ', arguments)}' did not exit within {timeoutMilliseconds}ms and was terminated.");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet {string.Join(' ', arguments)}' failed with exit code {process.ExitCode}.{Environment.NewLine}{stderr}{stdout}");
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
