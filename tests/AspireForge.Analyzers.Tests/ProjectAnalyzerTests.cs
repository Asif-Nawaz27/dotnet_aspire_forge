using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Tests;

public class ProjectAnalyzerTests
{
    private const string MinimalCsproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    [Fact]
    public async Task AnalyzeAsync_CollectsIssues_AndSkipsRulesThatReturnNull()
    {
        using var project = TestProject.Create("public class C { }");
        File.WriteAllText(project.Context.ProjectFile, MinimalCsproj);

        var rules = new IAnalysisRule[] { new FakeRule("A", fires: true), new FakeRule("B", fires: false) };
        var analyzer = new ProjectAnalyzer(rules);

        var result = await analyzer.AnalyzeAsync(project.Context.ProjectFile);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("A", issue.RuleId);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesTheProjectFileNameAsTheProjectName()
    {
        using var project = TestProject.Create("public class C { }", projectName: "SomeApi");
        File.WriteAllText(project.Context.ProjectFile, MinimalCsproj);

        var analyzer = new ProjectAnalyzer([]);

        var result = await analyzer.AnalyzeAsync(project.Context.ProjectFile);

        Assert.Equal("SomeApi", result.ProjectName);
    }

    private sealed class FakeRule(string id, bool fires) : IAnalysisRule
    {
        public string Id => id;

        public string Title => id;

        public Severity DefaultSeverity => Severity.Warning;

        public Task<AnalysisIssue?> EvaluateAsync(ProjectContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(fires ? new AnalysisIssue(id, id, id, DefaultSeverity) : null);
    }
}
