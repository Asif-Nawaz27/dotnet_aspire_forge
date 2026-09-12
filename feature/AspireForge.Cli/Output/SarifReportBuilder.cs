using System.Reflection;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Output;

public static class SarifReportBuilder
{
    private const string SchemaUri =
        "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/main/Schemata/sarif-schema-2.1.0.json";

    public static SarifLog Build(AnalysisResult result, IReadOnlyList<IAnalysisRule> rules)
    {
        var toolVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

        var ruleDescriptors = rules
            .Select(rule => new SarifReportingDescriptor(rule.Id, rule.Title, new SarifMessage(rule.Title)))
            .ToList();

        var results = result.Issues
            .Select(issue => new SarifResult(
                issue.RuleId,
                ToSarifLevel(issue.Severity),
                new SarifMessage(issue.Description),
                BuildLocations(issue.FilePath)))
            .ToList();

        var driver = new SarifToolDriver("AspireForge", toolVersion, ruleDescriptors);
        var run = new SarifRun(new SarifTool(driver), results);

        return new SarifLog(SchemaUri, "2.1.0", [run]);
    }

    private static IReadOnlyList<SarifLocation> BuildLocations(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return [];
        }

        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), filePath).Replace('\\', '/');
        var lineCount = Math.Max(1, File.ReadAllLines(filePath).Length);
        var region = new SarifRegion(1, lineCount);

        return [new SarifLocation(new SarifPhysicalLocation(new SarifArtifactLocation(relativePath), region))];
    }

    private static string ToSarifLevel(Severity severity) => severity switch
    {
        Severity.Error or Severity.Critical => "error",
        Severity.Warning => "warning",
        _ => "note",
    };
}
