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

    // A package reference alone isn't proof of a violation - AspireForge's own `add postgres`, for
    // example, pins Microsoft.EntityFrameworkCore on the Api project purely to keep its version in
    // sync with Infrastructure's (see EfCoreDatabaseFeatureInstaller), with no persistence code of
    // its own. Only flag it when the Api project's OWN source (not a referenced project's) actually
    // uses one of these types - a real violation, not a version-pinning side effect.
    private static readonly string[] PersistenceUsageTokens =
    [
        "DbContext",
        "IDbConnection",
        "SqlConnection",
        "NpgsqlConnection",
        "MongoClient",
        "IMongoCollection",
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

        var ownFiles = SourceFileHeuristics.OwnSourceFiles(context);

        if (!await SourceFileHeuristics.ContainsAnyAsync(ownFiles, cancellationToken, PersistenceUsageTokens))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            $"This API project references '{persistencePackage}' directly and uses it in its own code. Persistence access should live behind a dedicated data/infrastructure layer instead of being referenced from the API project.",
            DefaultSeverity,
            context.ProjectFile);
    }
}
