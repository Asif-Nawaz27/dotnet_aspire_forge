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
                BuildLocations(issue.FilePath, issue.LineNumber)))
            .ToList();

        var driver = new SarifToolDriver("AspireForge", toolVersion, ruleDescriptors);
        var run = new SarifRun(new SarifTool(driver), results);

        return new SarifLog(SchemaUri, "2.1.0", [run]);
    }

    private static IReadOnlyList<SarifLocation> BuildLocations(string? filePath, int? lineNumber)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return [];
        }

        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), filePath).Replace('\\', '/');

        // Point at the exact line when the rule knows it (so code scanning annotates that line);
        // otherwise the finding is about the file as a whole.
        var region = lineNumber is > 0
            ? new SarifRegion(lineNumber.Value, lineNumber.Value)
            : new SarifRegion(1, Math.Max(1, File.ReadAllLines(filePath).Length));

        return [new SarifLocation(new SarifPhysicalLocation(new SarifArtifactLocation(relativePath), region))];
    }

    private static string ToSarifLevel(Severity severity) => severity switch
    {
        Severity.Error or Severity.Critical => "error",
        Severity.Warning => "warning",
        _ => "note",
    };
}
