using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Architecture;

public sealed class ARCH001ApiDirectlyAccessesPersistenceLayer : IAnalysisRule
{
    private static readonly string[] PersistencePackagePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Dapper",
        "Npgsql",
        "Microsoft.Data.SqlClient",
        "System.Data.SqlClient",
        "MongoDB.Driver",
    ];

    public string Id => "ARCH001";

    public string Title => "API directly accesses persistence layer";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        var persistencePackage = context.PackageReferences.FirstOrDefault(package =>
            PersistencePackagePrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        if (persistencePackage is null)
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            $"This API project references '{persistencePackage}' directly. Persistence access should live behind a dedicated data/infrastructure layer instead of being referenced from the API project.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
