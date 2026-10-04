using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Deployment;

// A framework past end of support gets no security patches: every runtime CVE disclosed after that
// date stays open in production. Flags each target framework that is out of support (error) or ends
// support within WarningWindow (warning), so an upgrade can be planned before the deadline instead
// of after it.
//
// End-of-support dates come from Microsoft's lifecycle policies:
//   https://dotnet.microsoft.com/platform/support/policy/dotnet-core
//   https://learn.microsoft.com/lifecycle/products/microsoft-net-framework
// Add each new release here when it ships. Frameworks not listed (future versions, netstandard -
// a library contract rather than a runtime) are never flagged.
public sealed partial class DEP001UnsupportedTargetFramework(DateOnly? today = null) : IAnalysisRule
{
    public static readonly TimeSpan WarningWindow = TimeSpan.FromDays(183);

    private static readonly Dictionary<string, DateOnly> EndOfSupport = new(StringComparer.OrdinalIgnoreCase)
    {
        // .NET Core / .NET
        ["netcoreapp1.0"] = new(2019, 6, 27),
        ["netcoreapp1.1"] = new(2019, 6, 27),
        ["netcoreapp2.0"] = new(2018, 10, 1),
        ["netcoreapp2.1"] = new(2021, 8, 21),
        ["netcoreapp2.2"] = new(2019, 12, 23),
        ["netcoreapp3.0"] = new(2020, 3, 3),
        ["netcoreapp3.1"] = new(2022, 12, 13),
        ["net5.0"] = new(2022, 5, 10),
        ["net6.0"] = new(2024, 11, 12),
        ["net7.0"] = new(2024, 5, 14),
        ["net8.0"] = new(2026, 11, 10),
        // STS releases are supported for 24 months from .NET 9 onward, so .NET 9 ends with .NET 8.
        ["net9.0"] = new(2026, 11, 10),
        ["net10.0"] = new(2028, 11, 14),

        // .NET Framework (4.6.2 and later remain supported as Windows components)
        ["net40"] = new(2016, 1, 12),
        ["net403"] = new(2016, 1, 12),
        ["net45"] = new(2016, 1, 12),
        ["net451"] = new(2016, 1, 12),
        ["net452"] = new(2022, 4, 26),
        ["net46"] = new(2022, 4, 26),
        ["net461"] = new(2022, 4, 26),
    };

    public string Id => "DEP001";

    public string Title => "Unsupported .NET version";

    public Severity DefaultSeverity => Severity.Error;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var flagged = context.TargetFrameworks
            .Select(framework => (Framework: framework, EndOfSupport: Lookup(framework)))
            .Where(entry => entry.EndOfSupport is { } end && end.AddDays(-WarningWindow.Days) <= now)
            .Select(entry => (entry.Framework, EndOfSupport: entry.EndOfSupport!.Value))
            .OrderBy(entry => entry.EndOfSupport)
            .ToList();

        if (flagged.Count == 0)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        var unsupported = flagged.Where(entry => entry.EndOfSupport <= now).ToList();
        var endingSoon = flagged.Where(entry => entry.EndOfSupport > now).ToList();

        var parts = new List<string>();

        if (unsupported.Count > 0)
        {
            parts.Add($"{Describe(unsupported, now)} "
                + (unsupported.Count == 1
                    ? "is out of support and no longer receives security patches"
                    : "are out of support and no longer receive security patches"));
        }

        if (endingSoon.Count > 0)
        {
            parts.Add($"{Describe(endingSoon, now)} {(endingSoon.Count == 1 ? "reaches" : "reach")} end of support soon");
        }

        var issue = new AnalysisIssue(
            Id,
            unsupported.Count > 0 ? Title : ".NET version nearing end of support",
            $"{string.Join("; ", parts)}. Retarget to a supported release - the current LTS is net10.0.",
            unsupported.Count > 0 ? DefaultSeverity : Severity.Warning,
            context.ProjectFile);

        return Task.FromResult<AnalysisIssue?>(issue);
    }

    private static string Describe(IEnumerable<(string Framework, DateOnly EndOfSupport)> entries, DateOnly now) =>
        string.Join(", ", entries.Select(entry => $"{entry.Framework} (support {(entry.EndOfSupport <= now ? "ended" : "ends")} {entry.EndOfSupport:yyyy-MM-dd})"));

    // OS-specific frameworks share the base framework's lifecycle: net8.0-windows ends with net8.0.
    private static DateOnly? Lookup(string framework)
    {
        var baseFramework = PlatformSuffix().Replace(framework.Trim(), string.Empty);
        return EndOfSupport.TryGetValue(baseFramework, out var end) ? end : null;
    }

    [GeneratedRegex(@"-[A-Za-z][\w.]*$")]
    private static partial Regex PlatformSuffix();
}
