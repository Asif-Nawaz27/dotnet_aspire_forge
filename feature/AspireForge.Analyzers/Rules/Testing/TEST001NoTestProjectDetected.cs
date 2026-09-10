using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Testing;

public sealed class TEST001NoTestProjectDetected : IAnalysisRule
{
    public string Id => "TEST001";

    public string Title => "No test project detected";

    public Severity DefaultSeverity => Severity.Warning;

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

        var hasMatchingTestProject = Directory
            .EnumerateDirectories(testsDirectory)
            .Select(Path.GetFileName)
            .Any(name => name is not null
                && name.Contains(context.ProjectName, StringComparison.OrdinalIgnoreCase)
                && name.Contains("Test", StringComparison.OrdinalIgnoreCase));

        if (hasMatchingTestProject)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        return Task.FromResult<AnalysisIssue?>(new AnalysisIssue(
            Id,
            Title,
            $"No test project matching '{context.ProjectName}' was found under '{testsDirectory}'.",
            DefaultSeverity,
            context.ProjectFile));
    }
}
