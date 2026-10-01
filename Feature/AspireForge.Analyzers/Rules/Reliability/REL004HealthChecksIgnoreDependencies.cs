using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

// A bare AddHealthChecks() only proves the process is up. When the app depends on a database or
// Redis, readiness should fail while that dependency is down - otherwise the load balancer keeps
// routing traffic to an instance that can only return errors.
//
// Aspire client integrations (Aspire.Npgsql..., Aspire.StackExchange.Redis...) register their own
// health checks, so a project using one is treated as covered.
public sealed partial class REL004HealthChecksIgnoreDependencies : IAnalysisRule
{
    private static readonly string[] DependencyPackagePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.Data.SqlClient",
        "System.Data.SqlClient",
        "MongoDB.Driver",
        "StackExchange.Redis",
        "Microsoft.Extensions.Caching.StackExchangeRedis",
    ];

    public string Id => "REL004";

    public string Title => "Health checks don't check dependencies";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        var dependencyPackage = context.PackageReferences.FirstOrDefault(package =>
            DependencyPackagePrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        if (dependencyPackage is null
            || context.PackageReferences.Any(package => package.StartsWith("Aspire.", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (await SourceFileHeuristics.MatchesAsync(context, cancellationToken, DependencyHealthCheck()))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            $"{dependencyPackage} is referenced, but no dependency health check (AddDbContextCheck, AddNpgSql, AddSqlServer, AddRedis, ...) was found. "
                + "Readiness reports Healthy even while the database or cache is down.",
            DefaultSeverity,
            context.ProjectFile);
    }

    // Requires a following '(' or '<' so the health check AddRedis( doesn't collide with
    // AddRedisDistributedCache( or AddRedisClient(.
    [GeneratedRegex(@"\b(AddDbContextCheck|AddNpgSql|AddSqlServer|AddRedis|AddMongoDb|AddMySql|AddOracle|AddCheck)\s*[<(]")]
    private static partial Regex DependencyHealthCheck();
}
