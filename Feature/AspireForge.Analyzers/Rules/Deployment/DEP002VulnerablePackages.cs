using AspireForge.Analyzers.Vulnerabilities;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Deployment;

// Packages - direct or transitive - with a published security advisory, from
// `dotnet list package --vulnerable --include-transitive`. Opt-in (`aspireforge doctor --vulnerable`):
// it runs the dotnet CLI, restores the project if needed, and queries NuGet's vulnerability data, so
// it's slower than the other rules and needs network access.
//
// Advisory severity maps to the finding: Critical/High -> error, Moderate -> warning, Low -> suggestion.
// If the scan itself can't run (no network, restore failure), that's reported as an info finding
// rather than silently passing.
public sealed class DEP002VulnerablePackages(IVulnerablePackageScanner scanner) : IAnalysisRule
{
    private const int MaxPackagesInDescription = 5;

    public string Id => "DEP002";

    public string Title => "Vulnerable packages";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await scanner.ScanAsync(context.ProjectFile, cancellationToken);

        if (result.Problem is not null)
        {
            return new AnalysisIssue(
                Id,
                "Vulnerability scan couldn't run",
                $"The package vulnerability scan didn't complete: {result.Problem.TrimEnd('.', ' ')}. Make sure the project restores "
                    + "(`dotnet restore`) and the NuGet sources are reachable, then run doctor --vulnerable again.",
                Severity.Info,
                context.ProjectFile);
        }

        if (result.Vulnerabilities.Count == 0)
        {
            return null;
        }

        var findings = result.Vulnerabilities
            .OrderByDescending(v => Map(v.Severity))
            .ThenBy(v => v.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var listed = string.Join(", ", findings.Take(MaxPackagesInDescription).Select(Describe));
        var more = findings.Count > MaxPackagesInDescription
            ? $" (and {findings.Count - MaxPackagesInDescription} more)"
            : string.Empty;

        var hasTransitive = findings.Any(v => v.IsTransitive);

        return new AnalysisIssue(
            Id,
            Title,
            $"Packages with known security vulnerabilities: {listed}{more}. Upgrade each to a patched version"
                + (hasTransitive
                    ? "; for a transitive package, upgrade the direct dependency that brings it in, or add a direct "
                        + "reference to the patched version."
                    : ".")
                + " Details: dotnet list package --vulnerable --include-transitive.",
            Map(findings[0].Severity),
            context.ProjectFile);
    }

    private static string Describe(PackageVulnerability v) =>
        $"{v.PackageId} {v.ResolvedVersion} ({v.Severity.ToLowerInvariant()}{(v.IsTransitive ? ", transitive" : string.Empty)}"
            + (v.AdvisoryUrl.Length > 0 ? $", {v.AdvisoryUrl})" : ")");

    private static Severity Map(string advisorySeverity) => advisorySeverity.ToLowerInvariant() switch
    {
        "critical" or "high" => Severity.Error,
        "moderate" => Severity.Warning,
        "low" => Severity.Suggestion,
        _ => Severity.Warning,
    };
}
