using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Deployment;

// Flags package versions that aren't stable releases. A prerelease (-preview, -rc, -beta, -alpha, ...)
// carries no support commitment: APIs can still change, bugs are expected, and security fixes may only
// land in the next preview. A floating version (*, 1.*) resolves to whatever is newest at restore time,
// so two builds of the same commit can ship different code.
//
// Projects checked: the analysed project and every project it references, transitively - their
// packages ship in the same deployment. Versions come from:
//   - <PackageReference Version="..."> (attribute or child element), and VersionOverride
//   - central package management: <PackageVersion> in the nearest Directory.Packages.props
// Ranges are judged by their bounds ([1.0.0-beta, ) is a prerelease). Versions built from MSBuild
// properties ($(...)) can't be evaluated and are skipped, as are build-only packages
// (PrivateAssets="all" - analyzers, EF Core Design) because they never reach production.
public sealed partial class DEP004PrereleasePackages : IAnalysisRule
{
    private const int MaxPackagesInDescription = 5;

    public string Id => "DEP004";

    public string Title => "Prerelease or floating package versions";

    public Severity DefaultSeverity => Severity.Warning;

    public Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>([Path.GetFullPath(context.ProjectFile)]);

        while (pending.TryDequeue(out var projectFile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!visited.Add(projectFile) || Load(projectFile) is not { } project)
            {
                continue;
            }

            var directory = Path.GetDirectoryName(projectFile)!;
            var centralVersions = new Lazy<Dictionary<string, (string Version, string File, int? Line)>>(
                () => CentralVersions(directory));

            foreach (var reference in Elements(project, "PackageReference"))
            {
                var name = reference.Attribute("Include")?.Value;

                if (string.IsNullOrWhiteSpace(name) || IsBuildOnly(reference))
                {
                    continue;
                }

                var version = reference.Attribute("VersionOverride")?.Value
                    ?? reference.Attribute("Version")?.Value
                    ?? reference.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;

                if (version is not null)
                {
                    Check(findings, name, version, projectFile, LineOf(reference));
                }
                else if (centralVersions.Value.TryGetValue(name, out var central))
                {
                    Check(findings, name, central.Version, central.File, central.Line);
                }
            }

            foreach (var reference in Elements(project, "ProjectReference"))
            {
                if (reference.Attribute("Include")?.Value is { Length: > 0 } include)
                {
                    pending.Enqueue(Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar))));
                }
            }
        }

        // The same central version is reached from several projects - report each declaration once.
        findings = findings
            .DistinctBy(f => (f.Package.ToLowerInvariant(), f.File.ToLowerInvariant(), f.Line))
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Package, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (findings.Count == 0)
        {
            return Task.FromResult<AnalysisIssue?>(null);
        }

        var listed = string.Join(", ", findings.Take(MaxPackagesInDescription).Select(f => f.Describe()));
        var more = findings.Count > MaxPackagesInDescription
            ? $" (and {findings.Count - MaxPackagesInDescription} more)"
            : string.Empty;

        return Task.FromResult<AnalysisIssue?>(new AnalysisIssue(
            Id,
            Title,
            $"Packages that ship with this service aren't pinned to stable releases: {listed}{more}. Prereleases carry no "
                + "support commitment and can change or break before release; floating versions resolve differently on "
                + "each restore. Pin each package to an exact stable version.",
            findings[0].Severity,
            findings[0].File,
            findings[0].Line));
    }

    private static void Check(List<Finding> findings, string package, string version, string file, int? line)
    {
        if (version.Contains("$(", StringComparison.Ordinal))
        {
            return;
        }

        if (version.Contains('*'))
        {
            findings.Add(new Finding(package, version, file, line, Severity.Suggestion, "floating"));
        }
        else if (PrereleaseVersion().IsMatch(version))
        {
            findings.Add(new Finding(package, version, file, line, Severity.Warning, "prerelease"));
        }
    }

    // Nearest Directory.Packages.props above the project, as MSBuild imports it.
    private static Dictionary<string, (string Version, string File, int? Line)> CentralVersions(string projectDirectory)
    {
        var versions = new Dictionary<string, (string, string, int?)>(StringComparer.OrdinalIgnoreCase);

        for (var directory = new DirectoryInfo(projectDirectory); directory is not null; directory = directory.Parent)
        {
            var props = Path.Combine(directory.FullName, "Directory.Packages.props");

            if (!File.Exists(props))
            {
                continue;
            }

            if (Load(props) is { } document)
            {
                foreach (var entry in Elements(document, "PackageVersion"))
                {
                    if (entry.Attribute("Include")?.Value is { Length: > 0 } name && entry.Attribute("Version")?.Value is { } version)
                    {
                        versions[name] = (version, props, LineOf(entry));
                    }
                }
            }

            break;
        }

        return versions;
    }

    private static bool IsBuildOnly(XElement reference)
    {
        var privateAssets = reference.Attribute("PrivateAssets")?.Value
            ?? reference.Elements().FirstOrDefault(e => e.Name.LocalName == "PrivateAssets")?.Value;

        return string.Equals(privateAssets?.Trim(), "all", StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument? Load(string file)
    {
        try
        {
            return File.Exists(file) ? XDocument.Load(file, LoadOptions.SetLineInfo) : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    // Matched by local name so files using the legacy MSBuild XML namespace are read too.
    private static IEnumerable<XElement> Elements(XDocument document, string localName) =>
        document.Descendants().Where(element => element.Name.LocalName == localName);

    private static int? LineOf(XElement element) =>
        ((IXmlLineInfo)element).HasLineInfo() ? ((IXmlLineInfo)element).LineNumber : null;

    private sealed record Finding(string Package, string Version, string File, int? Line, Severity Severity, string Kind)
    {
        public string Describe() =>
            $"{Package} {Version} ({Kind}, {System.IO.Path.GetFileName(File)}{(Line is { } line ? $":{line}" : string.Empty)})";
    }

    // A SemVer prerelease label on any version in the string - a plain version or either bound of a range.
    [GeneratedRegex(@"\d+(?:\.\d+){1,3}-[0-9A-Za-z][0-9A-Za-z.-]*")]
    private static partial Regex PrereleaseVersion();
}
