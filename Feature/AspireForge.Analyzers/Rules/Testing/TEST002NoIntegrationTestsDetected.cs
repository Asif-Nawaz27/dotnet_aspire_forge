using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Testing;

public sealed class TEST002NoIntegrationTestsDetected : IAnalysisRule
{
    public string Id => "TEST002";

    public string Title => "No integration tests detected";

    public Severity DefaultSeverity => Severity.Info;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ProjectName.Contains("Test", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        var testsDirectory = TestProjectLocator.FindTestsDirectory(context.RootDirectory);

        if (testsDirectory is null)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        var hasIntegrationTests = Directory
            .EnumerateDirectories(testsDirectory)
            .Select(Path.GetFileName)
            .Any(name => name is not null && name.Contains("Integration", StringComparison.OrdinalIgnoreCase));

        if (hasIntegrationTests)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        return Task.FromResult<AnalysisIssue?>(new AnalysisIssue(
            Id,
            Title,
            $"No integration test project was found under '{testsDirectory}'.",
            DefaultSeverity,
            context.ProjectFile));
    }
}
