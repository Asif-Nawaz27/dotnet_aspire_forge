using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Observability;

// Flags Debug/Trace (Serilog: Debug/Verbose) minimum log levels in configuration that reaches
// production. Verbose logging multiplies log volume - and with it ingestion cost and noise - and Debug
// messages routinely include request bodies, query parameters, and other personal data that never
// appear at Information.
//
// Settings checked (appsettings*.json except appsettings.Development.json, via JsonSettingsReader):
//   Logging:LogLevel:<category>                 Microsoft.Extensions.Logging
//   Logging:<Provider>:LogLevel:<category>      provider-specific overrides (Console, OpenTelemetry, ...)
//   Serilog:MinimumLevel / :Default / :Override:<category>
// The Default category is a warning; a single category turned up is a suggestion - sometimes
// deliberate, but worth knowing about.
//
// Configuration layers: appsettings.{Environment}.json overrides appsettings.json, so a verbose value
// in the base file isn't reported when appsettings.Production.json sets the same key to a quieter level.
public sealed partial class OBS007VerboseLoggingByDefault : IAnalysisRule
{
    private const string ProductionFileName = "appsettings.Production.json";

    public string Id => "OBS007";

    public string Title => "Verbose logging by default";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var settingsByFile = new Dictionary<string, List<JsonSettingsReader.Setting>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in JsonSettingsReader.ConfigurationFiles(context.RootDirectory))
        {
            settingsByFile[file] = JsonSettingsReader.ReadStrings(await File.ReadAllBytesAsync(file, cancellationToken));
        }

        var production = settingsByFile
            .FirstOrDefault(entry => Path.GetFileName(entry.Key).Equals(ProductionFileName, StringComparison.OrdinalIgnoreCase))
            .Value ?? [];

        var findings = new List<Finding>();

        foreach (var (file, settings) in settingsByFile)
        {
            var isProductionFile = Path.GetFileName(file).Equals(ProductionFileName, StringComparison.OrdinalIgnoreCase);

            foreach (var setting in settings)
            {
                if (Classify(setting) is not { } category)
                {
                    continue;
                }

                // A quieter value in appsettings.Production.json wins in production.
                if (!isProductionFile && production.Any(p =>
                        p.Path.Equals(setting.Path, StringComparison.OrdinalIgnoreCase) && Classify(p) is null))
                {
                    continue;
                }

                var severity = category.Equals("Default", StringComparison.OrdinalIgnoreCase) ? Severity.Warning : Severity.Suggestion;
                findings.Add(new Finding(file, setting.Line, setting.Path, setting.Value, severity));
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Line)
            .ToList();

        var listed = string.Join(", ", findings.Select(f => $"'{f.Path}' is {f.Value} ({Path.GetFileName(f.File)}:{f.Line})"));

        return new AnalysisIssue(
            Id,
            findings[0].Severity == Severity.Warning ? Title : "Verbose logging for a category",
            $"Configuration for deployed environments (outside appsettings.Development.json) logs at Debug/Trace: {listed}. That multiplies log volume and cost, "
                + "and Debug messages often include personal data. Use Information (or Warning) here, and keep verbose "
                + "levels in appsettings.Development.json or turn them on temporarily via environment variables.",
            findings[0].Severity,
            findings[0].File,
            findings[0].Line);
    }

    // The category name when the setting is a verbose minimum level, otherwise null.
    private static string? Classify(JsonSettingsReader.Setting setting)
    {
        var logging = LoggingLevelPath().Match(setting.Path);
        if (logging.Success)
        {
            return VerboseLoggingLevel().IsMatch(setting.Value) ? logging.Groups["category"].Value : null;
        }

        var serilog = SerilogLevelPath().Match(setting.Path);
        if (serilog.Success)
        {
            var category = serilog.Groups["category"].Success ? serilog.Groups["category"].Value : "Default";
            return VerboseSerilogLevel().IsMatch(setting.Value) ? category : null;
        }

        return null;
    }

    private sealed record Finding(string File, int Line, string Path, string Value, Severity Severity);

    // Logging:LogLevel:X or Logging:Provider:LogLevel:X
    [GeneratedRegex(@"^Logging(?::[^:]+)?:LogLevel:(?<category>[^:]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LoggingLevelPath();

    // Serilog:MinimumLevel, Serilog:MinimumLevel:Default, Serilog:MinimumLevel:Override:X
    [GeneratedRegex(@"^Serilog:MinimumLevel(?::Default|:Override:(?<category>.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex SerilogLevelPath();

    [GeneratedRegex(@"^\s*(?:Trace|Debug)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex VerboseLoggingLevel();

    [GeneratedRegex(@"^\s*(?:Verbose|Debug)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex VerboseSerilogLevel();
}
