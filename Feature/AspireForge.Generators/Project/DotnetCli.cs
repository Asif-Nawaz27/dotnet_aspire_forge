using System.Diagnostics;
using System.Text;

namespace AspireForge.Generators.Project;

public static class DotnetCli
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    // How long to keep reading output after the process has exited. Normally the pipes close the moment
    // it exits; they only stay open when something it started is still holding them (see RunProcess).
    private static readonly TimeSpan DefaultOutputGracePeriod = TimeSpan.FromSeconds(10);

    internal static readonly string[] InheritedMSBuildVariables =
    [
        "MSBuildExtensionsPath",
        "MSBuildSDKsPath",
        "MSBUILD_EXE_PATH",
    ];

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

        // `dotnet` sets these for the MSBuild it runs, pointing at its own SDK, and every process it starts
        // inherits them: under `dotnet test` or `dotnet run -- new`, that includes this one. Passed on, they
        // make a child command build with the parent's SDK instead of the one it would choose for its own
        // directory, so a project the newest SDK created failed to build with an older one. Removing them
        // lets each command pick its SDK the way it does when a developer runs it.
        foreach (var variable in InheritedMSBuildVariables)
        {
            startInfo.Environment.Remove(variable);
        }

        var commandLine = $"{fileName} {string.Join(' ', arguments)}";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{commandLine}'.");

        // Both streams are read concurrently, as they arrive: a child that fills one pipe's buffer while
        // nothing reads it would block forever. Each gets its own thread rather than a thread-pool
        // callback, so a busy thread pool can't delay the reads past the grace period below.
        var stdout = new PipeReader(process.StandardOutput);
        var stderr = new PipeReader(process.StandardError);

        var timeoutMilliseconds = (int)(timeout ?? DefaultTimeout).TotalMilliseconds;

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            TryKillProcessTree(process);
            throw new TimeoutException($"'{commandLine}' did not exit within {timeoutMilliseconds}ms and was terminated.");
        }

        // The process has exited; its output is complete once the pipes close, or once the grace period
        // is over, if something it started is still holding them.
        var graceEnds = DateTime.UtcNow + (outputGracePeriod ?? DefaultOutputGracePeriod);
        stdout.WaitForEnd(graceEnds);
        stderr.WaitForEnd(graceEnds);

        var output = stdout.Text;
        var errors = stderr.Text;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{commandLine}' failed with exit code {process.ExitCode}.{Environment.NewLine}{errors}{output}");
        }

        return output;
    }

    // Reads a redirected stream to its end on a dedicated background thread. If something keeps the pipe
    // open forever, the thread stays blocked in Read; it's a background thread, so it never keeps the
    // process alive.
    private sealed class PipeReader
    {
        private readonly StringBuilder _text = new();
        private readonly Thread _thread;

        public PipeReader(StreamReader reader)
        {
            _thread = new Thread(() => ReadToEnd(reader)) { IsBackground = true, Name = "DotnetCli output" };
            _thread.Start();
        }

        public string Text
        {
            get
            {
                lock (_text)
                {
                    return _text.ToString();
                }
            }
        }

        public void WaitForEnd(DateTime deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            _thread.Join(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        }

        private void ReadToEnd(StreamReader reader)
        {
            var buffer = new char[4096];

            try
            {
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    lock (_text)
                    {
                        _text.Append(buffer, 0, read);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                // The process was disposed while the pipe was still held open; what was read is kept.
            }
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
