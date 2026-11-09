using AspireForge.Core.Analysis;
using AspireForge.Core.Configuration;

namespace AspireForge.Analyzers.Rules.Architecture;

public enum ArchitectureSource
{
    Configured,
    Detected,
    NotRecognised,
}

public sealed record ArchitectureResolution(ArchitectureModel? Model, ArchitectureSource Source)
{
    // "clean (detected)", "layered (from config)", "not recognised"
    public string Describe() => Source switch
    {
        ArchitectureSource.Configured => $"{Model!.Name} (from config)",
        ArchitectureSource.Detected => $"{Model!.Name} (detected)",
        _ => "not recognised",
    };
}

// Marks a rule that only means something once the architecture is known. When it isn't, doctor shows
// the rule as skipped instead of passed - a tick for a check that never ran would be a false pass.
public interface IRequiresArchitecture;

// Which architecture a project uses: the configured one, or else whichever style's signature names
// appear most often among the projects it references. A tie, including none at all, is not recognised:
// guessing would check a project against rules it was never meant to follow.
public static class ArchitectureResolver
{
    public static ArchitectureResolution Resolve(ProjectContext context, CancellationToken cancellationToken = default) =>
        Resolve(context.ProjectFile, context.Architecture, cancellationToken);

    public static ArchitectureResolution Resolve(
        string projectFile, ArchitectureStyle? configured, CancellationToken cancellationToken = default)
    {
        if (configured is { } style)
        {
            return new ArchitectureResolution(ArchitectureModel.For(style), ArchitectureSource.Configured);
        }

        var segments = ProjectGraph.Walk(projectFile, cancellationToken)
            .Select(project => ArchitectureModel.LastSegment(project.Name))
            .ToList();

        var ranked = ArchitectureModel.All
            .Select(model => (Model: model, Score: segments.Count(model.Signatures.Contains)))
            .OrderByDescending(candidate => candidate.Score)
            .ToList();

        return ranked[0].Score > 0 && ranked[0].Score > ranked[1].Score
            ? new ArchitectureResolution(ranked[0].Model, ArchitectureSource.Detected)
            : new ArchitectureResolution(null, ArchitectureSource.NotRecognised);
    }
}
