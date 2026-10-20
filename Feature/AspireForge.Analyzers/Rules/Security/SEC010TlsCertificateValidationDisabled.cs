using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// Flags code that accepts any TLS certificate. The connection is still encrypted, but to whoever
// answers - an attacker on the network path can present their own certificate and read or alter all
// traffic (man-in-the-middle). This is almost always a "temporary" fix for a self-signed certificate
// in development that then ships.
//
// Errors - every outbound TLS certificate is accepted:
//   HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
//   ServerCertificateCustomValidationCallback / RemoteCertificateValidationCallback /
//   ServicePointManager.ServerCertificateValidationCallback set to a callback that always returns true
//   (`(_, _, _, _) => true`, `=> { return true; }`, `delegate { return true; }`)
// Warning - the database connection skips certificate validation:
//   TrustServerCertificate=true / Trust Server Certificate=true in a connection string (C# source or
//   appsettings*.json other than appsettings.Development.json)
//
// A callback that actually inspects the certificate (e.g. `errors == SslPolicyErrors.None`), or a
// method group whose body isn't visible here, is not flagged. Code inside a development-only guard
// (`if (env.IsDevelopment())`, `#if DEBUG`) is allowed.
public sealed partial class SEC010TlsCertificateValidationDisabled : IAnalysisRule
{
    private const int MaxFindingsInDescription = 3;

    public string Id => "SEC010";

    public string Title => "TLS certificate validation disabled";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match match in AcceptAnyValidator().Matches(code))
            {
                AddUnlessDevelopmentOnly(findings, file, code, match.Index, Severity.Error,
                    "DangerousAcceptAnyServerCertificateValidator accepts every server certificate");
            }

            foreach (Match match in AlwaysTrueCallback().Matches(code))
            {
                var scope = match.Groups["name"].Value == "ServerCertificateValidationCallback"
                    ? " for every connection in the process"
                    : string.Empty;

                AddUnlessDevelopmentOnly(findings, file, code, match.Index, Severity.Error,
                    $"{match.Groups["name"].Value} always returns true, accepting every certificate{scope}");
            }

            foreach (Match match in TrustServerCertificate().Matches(code))
            {
                AddUnlessDevelopmentOnly(findings, file, code, match.Index, Severity.Warning,
                    "TrustServerCertificate=true skips the database server's certificate check");
            }
        }

        foreach (var file in ConfigurationFiles(context.RootDirectory))
        {
            var text = await File.ReadAllTextAsync(file, cancellationToken);

            foreach (Match match in TrustServerCertificate().Matches(text))
            {
                findings.Add(new Finding(file, SourceFileHeuristics.LineOf(text, match.Index), Severity.Warning,
                    "TrustServerCertificate=true skips the database server's certificate check"));
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Line)
            .ToList();

        var listed = string.Join("; ", findings.Take(MaxFindingsInDescription).Select(finding => finding.Describe()));
        var more = findings.Count > MaxFindingsInDescription
            ? $" (and {findings.Count - MaxFindingsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"TLS certificate validation is switched off: {listed}{more}. Anyone on the network path can present "
                + "their own certificate and read or change the traffic. Trust the specific certificate instead (install "
                + "the CA, or compare its thumbprint in the callback), and keep any bypass inside an IsDevelopment() check.",
            findings[0].Severity,
            findings[0].File,
            findings[0].Line);
    }

    private static void AddUnlessDevelopmentOnly(
        List<Finding> findings, string file, string code, int index, Severity severity, string message)
    {
        if (!DevelopmentGuard.Covers(code, index))
        {
            findings.Add(new Finding(file, SourceFileHeuristics.LineOf(code, index), severity, message));
        }
    }

    // Development settings are expected to trust local self-signed database certificates.
    private static IEnumerable<string> ConfigurationFiles(string rootDirectory) =>
        Directory.Exists(rootDirectory)
            ? Directory.EnumerateFiles(rootDirectory, "appsettings*.json", SearchOption.TopDirectoryOnly)
                .Where(file => !Path.GetFileName(file).Equals("appsettings.Development.json", StringComparison.OrdinalIgnoreCase))
            : [];

    private sealed record Finding(string File, int Line, Severity Severity, string Message)
    {
        public string Describe() => $"{Message} ({Path.GetFileName(File)}:{Line})";
    }

    [GeneratedRegex(@"\bDangerousAcceptAnyServerCertificateValidator\b")]
    private static partial Regex AcceptAnyValidator();

    // name = <lambda or anonymous method whose only result is `true`>, with = or +=.
    [GeneratedRegex(
        @"\b(?<name>ServerCertificateCustomValidationCallback|RemoteCertificateValidationCallback|ServerCertificateValidationCallback)\s*\+?=(?!=)\s*"
        + @"(?:delegate\s*(?:\([^)]*\))?\s*\{\s*return\s+true\s*;\s*\}"
        + @"|(?:\([^)]*\)|\w+)\s*=>\s*(?:true\b|\{\s*return\s+true\s*;\s*\}))")]
    private static partial Regex AlwaysTrueCallback();

    [GeneratedRegex(@"\bTrust\s*Server\s*Certificate\s*=\s*(?:true|yes)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TrustServerCertificate();
}
