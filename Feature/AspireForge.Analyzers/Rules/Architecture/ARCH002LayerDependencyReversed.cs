using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Architecture;

// The dependency rule: references only point the way the architecture allows, and never back.
//   clean    Presentation -> Infrastructure -> Application -> Domain   (what `aspireforge new` generates)
//   layered  Presentation -> Business -> Data Access -> Entities
// A reversed reference (Domain -> Infrastructure, Data Access -> Business, ...) compiles fine, then
// slowly welds business logic to a database, framework, or host, which is exactly what the layering
// exists to prevent.
//
// Walks the project-reference graph from the analysed project (so running `doctor` on the Api checks
// every layer it pulls in) and classifies each project by the last segment of its name - see
// ArchitectureModel for the names each style uses. Anything else (ServiceDefaults, Shared, Contracts,
// AppHost, ...Core) is unclassified and ignored - "Core" in particular means the domain in some
// templates and a shared utilities project in others. When the architecture isn't known, nothing is
// checked.
public sealed class ARCH002LayerDependencyReversed : IAnalysisRule, IRequiresArchitecture
{
    private const int MaxViolationsInDescription = 4;

    public string Id => "ARCH002";

    public string Title => "Layer dependency reversed";

    public Severity DefaultSeverity => Severity.Warning;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (ArchitectureResolver.Resolve(context, cancellationToken).Model is not { } model)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        var violations = new List<Violation>();

        foreach (var project in ProjectGraph.Walk(context.ProjectFile, cancellationToken))
        {
            if (model.Classify(project.Name) is not { } from)
            {
                continue;
            }

            foreach (var reference in ProjectGraph.Elements(project.Document, "ProjectReference"))
            {
                var toName = Path.GetFileNameWithoutExtension(ProjectGraph.ResolveReference(project.File, reference));

                // Inward means a lower rank; pointing at a higher one reverses the dependency.
                if (model.Classify(toName) is { } to && to.Rank > from.Rank)
                {
                    violations.Add(new Violation(project.File, ProjectGraph.LineOf(reference), project.Name, from.Name, toName, to.Name));
                }
            }
        }

        violations.RemoveAll(f => context.Suppressions.IsSuppressed(Id, f.ProjectFile, f.Line));

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
            $"Project references point outward: {listed}{more}. In {model.Name} architecture, dependencies must point "
                + $"inward ({model.Direction}). Move the code the inner layer needs into that layer, or define an "
                + "interface there and implement it in the outer one.",
            DefaultSeverity,
            violations[0].ProjectFile,
            violations[0].Line));
    }

    private sealed record Violation(string ProjectFile, int? Line, string From, string FromLayer, string To, string ToLayer)
    {
        public string Describe() =>
            $"{From} ({FromLayer}) references {To} ({ToLayer}) at {Path.GetFileName(ProjectFile)}{(Line is { } line ? $":{line}" : string.Empty)}";
    }
}
