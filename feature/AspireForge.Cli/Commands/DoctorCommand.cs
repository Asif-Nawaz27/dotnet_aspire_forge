using System.Text.Json;
using AspireForge.Analyzers;
using AspireForge.Cli.Output;
using AspireForge.Core.Analysis;

namespace AspireForge.Cli.Commands;

public class DoctorCommand(ConsoleRenderer renderer)
{
    private static readonly (string Category, string RuleId, string PassedMessage)[] Checks =
    [
        ("Security", "SEC001", "Authentication configured"),
        ("Security", "SEC002", "CORS policy restricts origins"),
        ("Security", "SEC003", "HTTPS configured"),
        ("Reliability", "REL001", "Health checks configured"),
        ("Reliability", "REL002", "Global exception handling"),
        ("Observability", "OBS001", "OpenTelemetry configured"),
        ("Observability", "OBS002", "Structured logging"),
        ("Testing", "TEST001", "Unit tests detected"),
        ("Testing", "TEST002", "Integration tests detected"),
        ("Architecture", "ARCH001", "No obvious dependency violations"),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<int> RunAsync(
        string path,
        Severity failOn = Severity.Error,
        OutputFormat format = OutputFormat.Text,
        CancellationToken cancellationToken = default)
    {
        var context = ProjectContextBuilder.Build(path);
        var analyzer = new ProjectAnalyzer(AnalysisRuleSet.CreateDefault());
        var result = await analyzer.AnalyzeAsync(path, cancellationToken);

        var shouldFail = result.Issues.Any(issue => issue.Severity >= failOn);

        if (format == OutputFormat.Json)
        {
            WriteJsonReport(result);
            return shouldFail ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;
        }

        var issuesByRuleId = result.Issues.ToDictionary(issue => issue.RuleId);

        renderer.WriteLine("AspireForge Doctor");
        renderer.WriteLine();
        renderer.WriteLine($"Project: {context.ProjectName}");
        renderer.WriteLine($"Framework: {context.TargetFramework ?? "unknown"}");

        var errorCount = 0;
        var warningCount = 0;
        var passedCount = 0;

        foreach (var category in Checks.Select(check => check.Category).Distinct())
        {
            renderer.WriteSectionHeader(category);

            foreach (var check in Checks.Where(check => check.Category == category))
            {
                if (issuesByRuleId.TryGetValue(check.RuleId, out var issue))
                {
                    var isError = issue.Severity is Severity.Error or Severity.Critical;
                    renderer.WriteCheck(isError ? "✗" : "⚠", issue.Title);

                    if (isError)
                    {
                        errorCount++;
                    }
                    else
                    {
                        warningCount++;
                    }
                }
                else
                {
                    renderer.WriteCheck("✓", check.PassedMessage);
                    passedCount++;
                }
            }
        }

        renderer.WriteLine();
        renderer.WriteSeparator();
        renderer.WriteLine($"Errors:   {errorCount}");
        renderer.WriteLine($"Warnings: {warningCount}");
        renderer.WriteLine($"Passed:   {passedCount}");
        renderer.WriteSeparator();
        renderer.WriteLine();

        var score = (int)Math.Round(10.0 * passedCount / Checks.Length);
        renderer.WriteLine($"Production Readiness: {score}/10");

        return shouldFail ? ExitCodes.AnalysisFoundErrors : ExitCodes.Success;
    }

    private void WriteJsonReport(AnalysisResult result)
    {
        var report = new DoctorJsonReport(
            result.ProjectName,
            result.Issues
                .Select(issue => new DoctorJsonIssue(
                    issue.RuleId, issue.Severity.ToString().ToLowerInvariant(), issue.Title))
                .ToList());

        renderer.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
    }
}
