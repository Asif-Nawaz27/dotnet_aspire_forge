using System.Text.Json;
using System.Text.RegularExpressions;

namespace AspireForge.Generators.Project;

// Adds NuGet packages without letting a new .NET release break the project.
//
// `dotnet add package X` with no version takes the newest release. For packages that ship with .NET -
// ASP.NET Core, Microsoft.Extensions, EF Core and its providers - the newest release targets the newest
// .NET: once .NET 11 shipped, Microsoft.AspNetCore.OpenApi 11.0.0 went into net10.0 projects, which then
// failed to restore (NU1202) and every generated project stopped building. These packages' major version
// is the .NET version they're built for, so this picks the newest stable release with the project's major.
// Patch releases, security fixes included, still come through. Other packages (OpenTelemetry, ...) have
// their own version numbers and are added at their newest release, as before.
internal static partial class FrameworkPackages
{
    private static readonly string[] AlignedPrefixes =
    [
        "Microsoft.AspNetCore.",
        "Microsoft.Extensions.",
        "Microsoft.EntityFrameworkCore",
    ];

    // Providers versioned with EF Core rather than on their own scheme.
    private static readonly string[] AlignedPackages =
    [
        "Npgsql.EntityFrameworkCore.PostgreSQL",
    ];

    public static void Add(string csprojPath, string packageId, string workingDirectory)
    {
        if (!IsFrameworkAligned(packageId) || TargetMajor(csprojPath) is not { } major)
        {
            DotnetCli.Run(["add", csprojPath, "package", packageId], workingDirectory);
            return;
        }

        // `dotnet package search` reads the same NuGet sources restore does, private feeds included.
        var json = DotnetCli.RunForOutput(["package", "search", packageId, "--exact-match", "--format", "json"], workingDirectory);
        var version = NewestStableWithMajor(VersionsFromSearch(json, packageId), major)
            ?? throw new InvalidOperationException(
                $"No stable {major}.x release of {packageId} was found on your NuGet sources. Its versions follow .NET's, "
                    + $"and {Path.GetFileName(csprojPath)} targets .NET {major}, so a newer release wouldn't restore.");

        DotnetCli.Run(["add", csprojPath, "package", packageId, "--version", version], workingDirectory);
    }

    public static bool IsFrameworkAligned(string packageId) =>
        AlignedPrefixes.Any(prefix => packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || AlignedPackages.Contains(packageId, StringComparer.OrdinalIgnoreCase);

    // The .NET major version a project targets: 10 for net10.0. With several targets, the lowest, since the
    // package has to work for all of them. Null for anything else (netstandard2.0, an MSBuild property),
    // in which case the package is added the old way rather than guessed at.
    // Takes a project file or, like `dotnet add`, a directory holding one - `new` passes the Api's directory.
    public static int? TargetMajor(string projectPath)
    {
        var csprojPath = Directory.Exists(projectPath)
            ? Directory.EnumerateFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault()
            : projectPath;

        if (csprojPath is null || !File.Exists(csprojPath))
        {
            return null;
        }

        var text = File.ReadAllText(csprojPath);
        var match = TargetFrameworkElement().Match(text);

        var majors = match.Success
            ? match.Groups["value"].Value
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(framework => NetCoreFramework().Match(framework))
                .Where(framework => framework.Success)
                .Select(framework => int.Parse(framework.Groups["major"].Value))
                .ToList()
            : [];

        return majors.Count > 0 ? majors.Min() : null;
    }

    // Every version of exactly this package, from every source, in `dotnet package search --format json` output.
    public static IEnumerable<string> VersionsFromSearch(string json, string packageId)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("searchResult", out var sources))
        {
            return [];
        }

        return sources.EnumerateArray()
            .Where(source => source.TryGetProperty("packages", out _))
            .SelectMany(source => source.GetProperty("packages").EnumerateArray())
            .Where(package => string.Equals(package.GetProperty("id").GetString(), packageId, StringComparison.OrdinalIgnoreCase))
            .Select(package => package.GetProperty("version").GetString())
            .OfType<string>()
            .ToList();
    }

    // Prereleases are skipped - a preview isn't what a new project should start on. Version.TryParse does
    // that: it rejects the '-' suffix ("10.0.3-rc.1"), so they never get a parsed version.
    // Null when there's none - FirstOrDefault rather than MaxBy, which throws on an empty sequence of tuples.
    public static string? NewestStableWithMajor(IEnumerable<string> versions, int major) =>
        versions
            .Select(version => version.Split('+')[0])
            .Select(version => (Text: version, Parsed: Version.TryParse(version, out var parsed) ? parsed : null))
            .Where(version => version.Parsed?.Major == major)
            .OrderByDescending(version => version.Parsed)
            .FirstOrDefault()
            .Text;

    [GeneratedRegex(@"<TargetFrameworks?>\s*(?<value>[^<]+?)\s*</TargetFrameworks?>")]
    private static partial Regex TargetFrameworkElement();

    // net5.0 and later; netcoreapp3.1 and netstandard aren't matched.
    [GeneratedRegex(@"^net(?<major>[5-9]|\d{2,})\.\d+", RegexOptions.IgnoreCase)]
    private static partial Regex NetCoreFramework();
}
