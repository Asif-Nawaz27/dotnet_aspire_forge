using System.Diagnostics;

namespace AspireForge.Analyzers.Vulnerabilities;

// Runs `dotnet list <project> package --vulnerable --include-transitive --format json`. The SDK checks
// every resolved package - including transitive ones pulled in through project references - against
// the vulnerability data published by the configured NuGet sources (nuget.org uses the GitHub Advisory
// Database). It restores the project first if needed, so it needs network access.
public sealed class DotnetListPackageScanner(TimeSpan? timeout = null) : IVulnerablePackageScanner
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(3);

    public async Task<VulnerabilityScanResult> ScanAsync(string projectFile, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in new[] { "list", projectFile, "package", "--vulnerable", "--include-transitive", "--format", "json" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Keep SDK first-run banners and telemetry notices out of the JSON on stdout.
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return VulnerabilityScanResult.Failed($"the dotnet CLI couldn't be started ({ex.Message})");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        var output = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var error = process.StandardError.ReadToEndAsync(timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return VulnerabilityScanResult.Failed($"`dotnet list package --vulnerable` didn't finish within {_timeout.TotalMinutes:0} minutes");
        }

        var json = await output;
        var result = VulnerabilityReportParser.Parse(json);

        // A non-zero exit without a parseable problem still means the scan didn't happen.
        if (process.ExitCode != 0 && result.Problem is null && result.Vulnerabilities.Count == 0)
        {
            var detail = (await error).Trim();
            return VulnerabilityScanResult.Failed(
                $"`dotnet list package --vulnerable` exited with code {process.ExitCode}{(detail.Length > 0 ? $": {detail}" : string.Empty)}");
        }

        return result;
    }
}
