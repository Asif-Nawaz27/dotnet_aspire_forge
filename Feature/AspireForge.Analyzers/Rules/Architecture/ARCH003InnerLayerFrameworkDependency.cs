using System.Xml.Linq;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Architecture;

// ARCH002's counterpart for packages: the inner layers must not depend on infrastructure frameworks
// either. A <PackageReference> from Domain to EF Core, or from Application to Npgsql or ASP.NET Core, is
// the same layering violation as a reversed project reference - it ties business logic to a database,
// web host, or cloud SDK - but ARCH002 can't see it.
//
// Walks the project-reference graph from the analysed project (ProjectGraph, the same walk and layer
// naming as ARCH002) and checks each inner project's references (LayerKind):
//
//   Core   Domain (clean), Entities (layered): EF Core (any Microsoft.EntityFrameworkCore* package),
//          database providers and drivers, ASP.NET Core (Microsoft.AspNetCore.* packages or
//          <FrameworkReference Microsoft.AspNetCore.App>), HTTP clients, caches, and messaging/cloud SDKs
//   Logic  Application (clean), Business (layered): the same, except plain EF Core - many templates
//          deliberately expose DbSet<T> from an interface here (IApplicationDbContext) - so only EF Core
//          *provider* packages (Microsoft.EntityFrameworkCore.SqlServer, Npgsql.EntityFrameworkCore.PostgreSQL,
//          ...) and Microsoft.EntityFrameworkCore.Design count there
//
// Microsoft.Extensions.* abstractions (logging, options, DI, caching) are fine in both. Build-only
// packages (PrivateAssets="all" - analyzers, source generators) are skipped.
public sealed class ARCH003InnerLayerFrameworkDependency : IAnalysisRule, IRequiresArchitecture
{
    private const int MaxFindingsInDescription = 5;

    // Database providers and drivers, the web host, HTTP clients, caches, messaging and cloud SDKs.
    private static readonly string[] InfrastructurePackagePrefixes =
    [
        "Npgsql",
        "Microsoft.Data.SqlClient",
        "System.Data.SqlClient",
        "Microsoft.Data.Sqlite",
        "MySql",
        "MySqlConnector",
        "Pomelo.",
        "Oracle.",
        "MongoDB.",
        "Dapper",
        "StackExchange.Redis",
        "Microsoft.AspNetCore.",
        "Microsoft.Extensions.Http",
        "Refit",
        "RestSharp",
        "RabbitMQ.",
        "MassTransit",
        "Confluent.Kafka",
        "Azure.Messaging.",
        "Azure.Storage.",
        "Azure.Data.",
        "AWSSDK.",
        "Google.Cloud.",
    ];

    // EF Core packages Application may still not take: the provider (it decides the database) and the
    // design-time tooling.
    private static readonly string[] EfCoreProviderPackages =
    [
        "Microsoft.EntityFrameworkCore.SqlServer",
        "Microsoft.EntityFrameworkCore.Sqlite",
        "Microsoft.EntityFrameworkCore.Cosmos",
        "Microsoft.EntityFrameworkCore.InMemory",
        "Microsoft.EntityFrameworkCore.Design",
        "Microsoft.EntityFrameworkCore.Tools",
    ];

    public string Id => "ARCH003";

    public string Title => "Inner layer depends on infrastructure framework";

    public Severity DefaultSeverity => Severity.Warning;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (ArchitectureResolver.Resolve(context, cancellationToken).Model is not { } model)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        var findings = new List<(string File, int? Line, string Project, string Layer, string Dependency)>();

        foreach (var project in ProjectGraph.Walk(context.ProjectFile, cancellationToken))
        {
            if (model.Classify(project.Name) is not { Kind: LayerKind.Core or LayerKind.Logic } layer)
            {
                continue;
            }

            foreach (var reference in ProjectGraph.Elements(project.Document, "PackageReference"))
            {
                var package = reference.Attribute("Include")!.Value;

                if (!IsBuildOnly(reference) && IsForbidden(package, layer.Kind))
                {
                    findings.Add((project.File, ProjectGraph.LineOf(reference), project.Name, layer.Name, package));
                }
            }

            foreach (var framework in ProjectGraph.Elements(project.Document, "FrameworkReference"))
            {
                var name = framework.Attribute("Include")!.Value;

                if (name.StartsWith("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add((project.File, ProjectGraph.LineOf(framework), project.Name, layer.Name, $"{name} (framework)"));
                }
            }
        }

        findings.RemoveAll(f => context.Suppressions.IsSuppressed(Id, f.File, f.Line));

        if (findings.Count == 0)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        var listed = string.Join("; ", findings.Take(MaxFindingsInDescription).Select(f =>
            $"{f.Project} ({f.Layer}) references {f.Dependency} at {Path.GetFileName(f.File)}{(f.Line is { } line ? $":{line}" : string.Empty)}"));
        var more = findings.Count > MaxFindingsInDescription
            ? $" (and {findings.Count - MaxFindingsInDescription} more)"
            : string.Empty;

        return Task.FromResult<AnalysisIssue?>(new AnalysisIssue(
            Id,
            Title,
            $"Inner layers depend on infrastructure frameworks: {listed}{more}. {Inner(model)} should only describe what "
                + $"the system does; move the code that needs these packages into {model.InfrastructureLayer.Name} (or the "
                + $"Api), behind an interface declared in {model.LogicLayer.Name}.",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line));
    }

    // "Domain and Application", "Entities and Business"
    private static string Inner(ArchitectureModel model) =>
        string.Join(" and ", model.Layers.Where(layer => layer.Kind != LayerKind.Outer).OrderBy(layer => layer.Rank).Select(layer => layer.Name));

    private static bool IsForbidden(string package, LayerKind kind)
    {
        if (package.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
        {
            return kind == LayerKind.Core
                || EfCoreProviderPackages.Contains(package, StringComparer.OrdinalIgnoreCase);
        }

        return InfrastructurePackagePrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBuildOnly(XElement reference)
    {
        var privateAssets = reference.Attribute("PrivateAssets")?.Value
            ?? reference.Elements().FirstOrDefault(e => e.Name.LocalName == "PrivateAssets")?.Value;

        return string.Equals(privateAssets?.Trim(), "all", StringComparison.OrdinalIgnoreCase);
    }
}
