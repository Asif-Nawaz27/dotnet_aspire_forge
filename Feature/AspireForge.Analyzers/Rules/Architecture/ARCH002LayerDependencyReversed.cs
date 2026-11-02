using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Architecture;

// The dependency rule of clean architecture - the layout `aspireforge new` generates: references
// point inward only. Presentation -> Infrastructure -> Application -> Domain, and never back out. A
// reversed reference (Domain -> Infrastructure, Application -> Api, ...) compiles fine, then slowly
// welds business logic to a database, framework, or host, which is exactly what the layering exists
// to prevent.
//
// Walks the project-reference graph from the analysed project (so running `doctor` on the Api checks
// every layer it pulls in) and classifies each project by the last segment of its name
// (see ProjectGraph.Classify):
//   Domain                       ...Domain
//   Application                  ...Application
//   Infrastructure               ...Infrastructure, ...Persistence
//   Presentation                 ...Api, ...WebApi, ...Web
// Anything else (ServiceDefaults, Shared, Contracts, AppHost, ...Core) is unclassified and ignored -
// "Core" in particular means the domain in some templates and a shared utilities project in others.
public sealed class ARCH002LayerDependencyReversed : IAnalysisRule
{
    private const int MaxViolationsInDescription = 4;

    public string Id => "ARCH002";

    public string Title => "Layer dependency reversed";

    public Severity DefaultSeverity => Severity.Warning;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<Violation>();

        foreach (var project in ProjectGraph.Walk(context.ProjectFile, cancellationToken))
        {
            foreach (var reference in ProjectGraph.Elements(project.Document, "ProjectReference"))
            {
                var toName = Path.GetFileNameWithoutExtension(ProjectGraph.ResolveReference(project.File, reference));

                // Inward means a lower layer; pointing at a higher one reverses the dependency.
                if (project.Layer is { } from && ProjectGraph.Classify(toName) is { } to && to > from)
                {
                    violations.Add(new Violation(project.File, ProjectGraph.LineOf(reference), project.Name, from, toName, to));
                }
            }
        }

        if (violations.Count == 0)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        var listed = string.Join("; ", violations.Take(MaxViolationsInDescription).Select(violation => violation.Describe()));
        var more = violations.Count > MaxViolationsInDescription
            ? $" (and {violations.Count - MaxViolationsInDescription} more)"
            : string.Empty;

        return Task.FromResult<AnalysisIssue?>(new AnalysisIssue(
            Id,
            Title,
            $"Project references point outward: {listed}{more}. Dependencies must point inward "
                + "(Presentation -> Infrastructure -> Application -> Domain). Move the code the inner layer needs into "
                + "that layer, or define an interface there and implement it in the outer one.",
            DefaultSeverity,
            violations[0].ProjectFile,
            violations[0].Line));
    }

    private sealed record Violation(string ProjectFile, int? Line, string From, ProjectLayer FromLayer, string To, ProjectLayer ToLayer)
    {
        public string Describe() =>
            $"{From} ({FromLayer}) references {To} ({ToLayer}) at {Path.GetFileName(ProjectFile)}{(Line is { } line ? $":{line}" : string.Empty)}";
    }
}
