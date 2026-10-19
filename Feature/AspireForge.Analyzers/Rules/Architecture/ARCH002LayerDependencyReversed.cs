using System.Xml;
using System.Xml.Linq;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Architecture;

// The dependency rule of clean architecture - the layout `aspireforge new` generates: references
// point inward only. Presentation -> Infrastructure -> Application -> Domain, and never back out. A
// reversed reference (Domain -> Infrastructure, Application -> Api, ...) compiles fine, then slowly
// welds business logic to a database, framework, or host, which is exactly what the layering exists
// to prevent.
//
// Walks the project-reference graph from the analysed project (so running `doctor` on the Api checks
// every layer it pulls in) and classifies each project by the last segment of its name:
//   Domain                       ...Domain
//   Application                  ...Application
//   Infrastructure               ...Infrastructure, ...Persistence
//   Presentation                 ...Api, ...WebApi, ...Web
// Anything else (ServiceDefaults, Shared, Contracts, AppHost, ...Core) is unclassified and ignored -
// "Core" in particular means the domain in some templates and a shared utilities project in others.
public sealed class ARCH002LayerDependencyReversed : IAnalysisRule
{
    private const int MaxViolationsInDescription = 4;

    private enum Layer
    {
        Domain,
        Application,
        Infrastructure,
        Presentation,
    }

    public string Id => "ARCH002";

    public string Title => "Layer dependency reversed";

    public Severity DefaultSeverity => Severity.Warning;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<Violation>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>([Path.GetFullPath(context.ProjectFile)]);

        while (pending.TryDequeue(out var projectFile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!visited.Add(projectFile) || LoadProject(projectFile) is not { } document)
            {
                continue;
            }

            var fromName = Path.GetFileNameWithoutExtension(projectFile);
            var fromLayer = Classify(fromName);
            var directory = Path.GetDirectoryName(projectFile)!;

            foreach (var reference in ProjectReferences(document))
            {
                var include = reference.Attribute("Include")!.Value;
                var referencedFile = Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar)));
                var toName = Path.GetFileNameWithoutExtension(referencedFile);
                var toLayer = Classify(toName);

                // Inward means a lower layer; pointing at a higher one reverses the dependency.
                if (fromLayer is { } from && toLayer is { } to && to > from)
                {
                    var line = ((IXmlLineInfo)reference).HasLineInfo() ? ((IXmlLineInfo)reference).LineNumber : (int?)null;
                    violations.Add(new Violation(projectFile, line, fromName, from, toName, to));
                }

                pending.Enqueue(referencedFile);
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

    private static Layer? Classify(string projectName)
    {
        var lastSegment = projectName[(projectName.LastIndexOf('.') + 1)..];

        return lastSegment.ToLowerInvariant() switch
        {
            "domain" => Layer.Domain,
            "application" => Layer.Application,
            "infrastructure" or "persistence" => Layer.Infrastructure,
            "api" or "webapi" or "web" => Layer.Presentation,
            _ => null,
        };
    }

    private static XDocument? LoadProject(string projectFile)
    {
        try
        {
            return File.Exists(projectFile) ? XDocument.Load(projectFile, LoadOptions.SetLineInfo) : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    // Matched by local name so projects using the legacy MSBuild XML namespace are read too.
    private static IEnumerable<XElement> ProjectReferences(XDocument document) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference"
                && !string.IsNullOrWhiteSpace(element.Attribute("Include")?.Value));

    private sealed record Violation(string ProjectFile, int? Line, string From, Layer FromLayer, string To, Layer ToLayer)
    {
        public string Describe() =>
            $"{From} ({FromLayer}) references {To} ({ToLayer}) at {Path.GetFileName(ProjectFile)}{(Line is { } line ? $":{line}" : string.Empty)}";
    }
}
