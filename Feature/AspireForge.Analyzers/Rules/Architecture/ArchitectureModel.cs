using AspireForge.Core.Configuration;

namespace AspireForge.Analyzers.Rules.Architecture;

// What the architecture rules check a layer for (ARCH003).
public enum LayerKind
{
    // The innermost layer: business concepts only. No database, web, or messaging packages at all.
    Core,

    // Business rules over the core. EF Core itself is allowed - many templates expose DbSet<T> from an
    // interface here - but not a database provider, the web host, or other infrastructure.
    Logic,

    // Where frameworks belong: data access and the host. Not checked for packages.
    Outer,
}

// One layer of a style. Rank orders the layers inside-out: a project may reference a layer of lower or
// equal rank, never a higher one (ARCH002). Suffixes are matched against the last segment of a project's
// name, ignoring case: "Orders.DataAccess" is in the layer whose suffixes include "dataaccess".
public sealed record ArchitectureLayer(string Name, int Rank, LayerKind Kind, IReadOnlyList<string> Suffixes);

// A style's layers, and the name segments that identify it when detecting. Signatures are the suffixes
// only this style uses: "Application" means clean, "BLL" means layered, but "Api" means nothing either way.
public sealed record ArchitectureModel(
    ArchitectureStyle Style,
    IReadOnlyList<ArchitectureLayer> Layers,
    IReadOnlySet<string> Signatures)
{
    public static readonly ArchitectureModel Clean = new(
        ArchitectureStyle.Clean,
        [
            new("Domain", 0, LayerKind.Core, ["domain"]),
            new("Application", 1, LayerKind.Logic, ["application"]),
            new("Infrastructure", 2, LayerKind.Outer, ["infrastructure", "persistence"]),
            new("Presentation", 3, LayerKind.Outer, ["api", "webapi", "web"]),
        ],
        // "domain" counts for clean, the default: a Domain project on its own is far more often clean
        // architecture than an N-tier entities project, which is usually called Entities or Models.
        new HashSet<string> { "domain", "application", "infrastructure" });

    public static readonly ArchitectureModel Layered = new(
        ArchitectureStyle.Layered,
        [
            new("Entities", 0, LayerKind.Core, ["entities", "models", "domain"]),
            new("Data Access", 1, LayerKind.Outer, ["dataaccess", "data", "dal", "repository", "repositories", "persistence"]),
            new("Business", 2, LayerKind.Logic, ["business", "businesslogic", "bll", "logic", "services"]),
            new("Presentation", 3, LayerKind.Outer, ["api", "webapi", "web", "ui", "presentation", "mvc"]),
        ],
        // Not "services" or "models": too common in projects of every style to say anything.
        new HashSet<string> { "business", "businesslogic", "bll", "logic", "dataaccess", "data", "dal", "repository", "repositories", "entities" });

    public static readonly IReadOnlyList<ArchitectureModel> All = [Clean, Layered];

    public string Name => Style.ToString().ToLowerInvariant();

    // "Presentation -> Infrastructure -> Application -> Domain", the way dependencies are allowed to point.
    public string Direction => string.Join(" -> ", Layers.OrderByDescending(layer => layer.Rank).Select(layer => layer.Name));

    // The layer that holds the logic, and the first one out from it where infrastructure belongs - what
    // the rules' messages tell people to move code between.
    public ArchitectureLayer LogicLayer => Layers.First(layer => layer.Kind == LayerKind.Logic);

    public ArchitectureLayer InfrastructureLayer => Layers.Where(layer => layer.Kind == LayerKind.Outer).MinBy(layer => layer.Rank)!;

    public ArchitectureLayer? Classify(string projectName)
    {
        var segment = LastSegment(projectName);

        return Layers.FirstOrDefault(layer => layer.Suffixes.Contains(segment));
    }

    public static ArchitectureModel For(ArchitectureStyle style) => All.Single(model => model.Style == style);

    internal static string LastSegment(string projectName) =>
        projectName[(projectName.LastIndexOf('.') + 1)..].ToLowerInvariant();
}
