using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Architecture;

// Flags an API project whose own code talks to the database directly instead of going through the
// Application/Infrastructure layers. Two ways in, both checked against the Api project's OWN source
// files (not referenced projects'):
//
//   1. The Api references a persistence package itself (EF Core, Dapper, Npgsql, SqlClient, MongoDB)
//      and its code uses that package's types.
//   2. The Api gets EF Core through a project reference - the normal clean-architecture layout, where
//      Api references Infrastructure - and its code works with the DbContext directly:
//        - a ...DbContext constructor parameter, field, property, or endpoint lambda parameter
//        - IDbContextFactory<...> or DbSet<...>
//        - GetRequiredService<...DbContext>() / GetService<...DbContext>() outside Program.cs/Startup.cs
//        - a class deriving from DbContext
//      No package reference is needed for this one - which is why checking packages alone misses it.
//
// The composition root is allowed: AddDbContext<...>() and other service registrations, and resolving
// the context in Program.cs/Startup.cs for startup work such as applying migrations.
public sealed partial class ARCH001ApiDirectlyAccessesPersistenceLayer : IAnalysisRule
{
    private const int MaxLocationsInDescription = 3;

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

    private static readonly string[] CompositionRootFiles = ["Program.cs", "Startup.cs"];

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

        var ownFiles = SourceFileHeuristics.OwnSourceFiles(context).ToList();

        var persistencePackage = context.PackageReferences.FirstOrDefault(package =>
            PersistencePackagePrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        if (persistencePackage is not null
            && await SourceFileHeuristics.ContainsAnyAsync(ownFiles, cancellationToken, PersistenceUsageTokens))
        {
            return new AnalysisIssue(
                Id,
                Title,
                $"This API project references '{persistencePackage}' directly and uses it in its own code. Persistence access should live behind a dedicated data/infrastructure layer instead of being referenced from the API project.",
                DefaultSeverity,
                context.ProjectFile);
        }

        var usages = await FindDbContextUsagesAsync(ownFiles, cancellationToken);

        usages.RemoveAll(f => context.Suppressions.IsSuppressed(Id, f.File, f.Line));

        if (usages.Count == 0)
        {
            return null;
        }

        var locations = string.Join(
            ", ",
            usages.Take(MaxLocationsInDescription).Select(u => $"{u.What} ({Path.GetFileName(u.File)}:{u.Line})"));
        var more = usages.Count > MaxLocationsInDescription
            ? $" (and {usages.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"API code works with the database directly: {locations}{more}. Even without its own EF Core package, the API "
                + "reaches the DbContext through its Infrastructure reference, tying endpoints to the schema and bypassing "
                + "the Application layer. Inject an Application service (or repository interface) instead, and keep the "
                + "DbContext inside Infrastructure.",
            DefaultSeverity,
            usages[0].File,
            usages[0].Line);
    }

    private static async Task<List<(string File, int Line, string What)>> FindDbContextUsagesAsync(
        IEnumerable<string> files, CancellationToken cancellationToken)
    {
        var usages = new List<(string File, int Line, string What)>();

        foreach (var file in files)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);
            var isCompositionRoot = CompositionRootFiles.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase);

            void Add(Match match, string what) =>
                usages.Add((file, SourceFileHeuristics.LineOf(code, match.Index), what));

            foreach (Match match in DbContextSubclass().Matches(code))
            {
                Add(match, $"{match.Groups["type"].Value} derives from DbContext");
            }

            foreach (Match match in DbContextDeclaration().Matches(code))
            {
                // In the composition root, `AppDbContext db = scope...GetRequiredService<...>();` is startup
                // work (migrations, seeding); only parameters - endpoint lambdas - count there.
                var isInitializedLocal = code[match.Index + match.Length] is '=' or ';';

                if (!(isCompositionRoot && isInitializedLocal))
                {
                    Add(match, $"{match.Groups["type"].Value} injected or declared");
                }
            }

            foreach (Match match in DbContextFactoryOrSet().Matches(code))
            {
                Add(match, $"{match.Groups["type"].Value}<...> used");
            }

            if (!isCompositionRoot)
            {
                foreach (Match match in DbContextServiceLocation().Matches(code))
                {
                    Add(match, $"{match.Groups["type"].Value} resolved from the service provider");
                }
            }
        }

        // A primary-constructor parameter and its use can land on one line - report each line once.
        return usages
            .GroupBy(u => (u.File, u.Line))
            .Select(group => group.First())
            .OrderBy(u => u.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.Line)
            .ToList();
    }

    // class AppDbContext : DbContext  /  : IdentityDbContext<User>  /  : Microsoft.EntityFrameworkCore.DbContext
    [GeneratedRegex(@"\bclass\s+(?<type>\w+)[^{;]*?:\s*(?:[\w.]+\.)?\w*DbContext\b")]
    private static partial Regex DbContextSubclass();

    // A variable/parameter/field/property declaration whose type ends in DbContext:
    // `AppDbContext db,`  `(AppDbContext db) =>`  `private readonly AppDbContext _db;`  `AppDbContext Db { get; }`
    // Not AddDbContext<AppDbContext>(...), DbContextOptions<...>, or typeof(AppDbContext).
    [GeneratedRegex(@"(?<![<\w.])(?:\w+\.)*(?<type>\w*DbContext)\??\s+@?\w+\s*(?=[,)=;{])")]
    private static partial Regex DbContextDeclaration();

    [GeneratedRegex(@"\b(?<type>IDbContextFactory|DbSet)\s*<\s*\w+\s*>")]
    private static partial Regex DbContextFactoryOrSet();

    [GeneratedRegex(@"\bGet(?:Required)?Service\s*<\s*(?<type>\w*DbContext)\s*>")]
    private static partial Regex DbContextServiceLocation();
}
