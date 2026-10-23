using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Deployment;

// Reviews the Dockerfile that builds this project. Not having one isn't a finding - plenty of apps
// deploy without containers, or build images with the .NET SDK (`dotnet publish /t:PublishContainer`).
//
// The Dockerfile is the nearest one found walking up from the project directory to the repository
// root (the directory containing .git), so a solution-level Dockerfile is found from the Api project.
// Names recognised: Dockerfile, *.Dockerfile, Dockerfile.*.
//
// Checks (the final stage is everything after the last FROM):
//   warning     the final stage runs as root - no USER instruction, or USER root / USER 0. The .NET
//               runtime images default to root; chiseled images (*-chiseled*) default to the non-root
//               `app` user and are exempt.
//   warning     the final stage is the SDK image (mcr.microsoft.com/dotnet/sdk) - compilers and build
//               tools ship to production: a far larger image and attack surface.
//   suggestion  a base image is unpinned (`:latest` or no tag), so the same Dockerfile builds
//               different images over time. Stage aliases, `scratch`, and ${ARG} images are skipped.
//   suggestion  `COPY . .` / `ADD . .` with no .dockerignore next to the Dockerfile - the build context
//               then includes .git, bin/obj, and local secrets such as appsettings.Development.json.
public sealed partial class DEP003ContainerProblems : IAnalysisRule
{
    public string Id => "DEP003";

    public string Title => "Container image problems";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var dockerfile = FindDockerfile(context.RootDirectory);

        if (dockerfile is null)
        {
            return null;
        }

        var instructions = Parse(await File.ReadAllLinesAsync(dockerfile, cancellationToken));
        var findings = Review(instructions, Path.GetDirectoryName(dockerfile)!);

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings.OrderByDescending(f => f.Severity).ThenBy(f => f.Line).ToList();
        var name = Path.GetFileName(dockerfile);

        return new AnalysisIssue(
            Id,
            Title,
            $"{name} has container problems: {string.Join("; ", findings.Select(f => $"{f.Message} ({name}:{f.Line})"))}.",
            findings[0].Severity,
            dockerfile,
            findings[0].Line);
    }

    private static List<Finding> Review(List<Instruction> instructions, string dockerfileDirectory)
    {
        var findings = new List<Finding>();
        var froms = instructions.Where(i => i.Keyword == "FROM").ToList();

        if (froms.Count == 0)
        {
            return findings;
        }

        var stageAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var from in froms)
        {
            var (image, alias) = ParseFrom(from.Arguments);

            if (IsUnpinned(image, stageAliases))
            {
                findings.Add(new Finding(from.Line, Severity.Suggestion,
                    $"base image '{image}' isn't pinned to a version, so rebuilds can silently pick up a different image"));
            }

            if (alias is not null)
            {
                stageAliases.Add(alias);
            }
        }

        var finalFrom = froms[^1];
        var (finalImage, _) = ParseFrom(finalFrom.Arguments);
        var finalStage = instructions.Where(i => i.Line > finalFrom.Line).ToList();

        if (finalImage.Contains("mcr.microsoft.com/dotnet/sdk", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding(finalFrom.Line, Severity.Warning,
                "the final stage is the .NET SDK image, shipping compilers and build tools to production - publish in a build stage and run on mcr.microsoft.com/dotnet/aspnet"));
        }

        var lastUser = finalStage.LastOrDefault(i => i.Keyword == "USER");

        if (lastUser is not null && RootUser().IsMatch(lastUser.Arguments))
        {
            findings.Add(new Finding(lastUser.Line, Severity.Warning,
                "the container runs as root - a compromised process gets full control of the container"));
        }
        else if (lastUser is null && !finalImage.Contains("chiseled", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new Finding(finalFrom.Line, Severity.Warning,
                "the final stage has no USER instruction, so the container runs as root - add `USER $APP_UID` (the non-root user the .NET images ship)"));
        }

        if (!File.Exists(Path.Combine(dockerfileDirectory, ".dockerignore"))
            && instructions.FirstOrDefault(i => i.Keyword is "COPY" or "ADD" && CopiesWholeContext().IsMatch(i.Arguments)) is { } copyAll)
        {
            findings.Add(new Finding(copyAll.Line, Severity.Suggestion,
                "the whole build context is copied with no .dockerignore, so .git, bin/obj, and local secrets such as appsettings.Development.json are sent into the build"));
        }

        return findings;
    }

    private static bool IsUnpinned(string image, HashSet<string> stageAliases)
    {
        if (image.Length == 0 || image.Equals("scratch", StringComparison.OrdinalIgnoreCase)
            || image.Contains('$') || stageAliases.Contains(image) || image.Contains('@'))
        {
            return false; // empty, scratch, ${ARG}, an earlier stage, or pinned by digest
        }

        var lastSegment = image[(image.LastIndexOf('/') + 1)..];
        var colon = lastSegment.IndexOf(':');

        return colon < 0 || lastSegment[(colon + 1)..].Equals("latest", StringComparison.OrdinalIgnoreCase);
    }

    // FROM [--platform=...] image[:tag] [AS alias]
    private static (string Image, string? Alias) ParseFrom(string arguments)
    {
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.StartsWith("--", StringComparison.Ordinal))
            .ToList();

        var image = parts.Count > 0 ? parts[0] : string.Empty;
        var alias = parts.Count >= 3 && parts[1].Equals("AS", StringComparison.OrdinalIgnoreCase) ? parts[2] : null;
        return (image, alias);
    }

    // Joins `\` line continuations and drops comments and parser directives, keeping each
    // instruction's starting line number.
    private static List<Instruction> Parse(string[] lines)
    {
        var instructions = new List<Instruction>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var startLine = i + 1;
            var text = line;

            while (text.EndsWith('\\') && i + 1 < lines.Length)
            {
                text = text[..^1] + " " + lines[++i].Trim();
            }

            var space = text.IndexOfAny([' ', '\t']);
            var keyword = (space < 0 ? text : text[..space]).ToUpperInvariant();
            var arguments = space < 0 ? string.Empty : text[(space + 1)..].Trim();

            instructions.Add(new Instruction(startLine, keyword, arguments));
        }

        return instructions;
    }

    private static string? FindDockerfile(string projectDirectory)
    {
        for (var directory = new DirectoryInfo(projectDirectory); directory is not null; directory = directory.Parent)
        {
            if (!directory.Exists)
            {
                continue;
            }

            var dockerfile = directory.EnumerateFiles()
                .Where(file => IsDockerfileName(file.Name))
                .OrderBy(file => file.Name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (dockerfile is not null)
            {
                return dockerfile.FullName;
            }

            // Don't look above the repository root.
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsDockerfileName(string name) =>
        name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".Dockerfile", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase);

    private sealed record Instruction(int Line, string Keyword, string Arguments);

    private sealed record Finding(int Line, Severity Severity, string Message);

    // USER root, USER 0, USER root:root, USER 0:0
    [GeneratedRegex(@"^(?:root|0)(?::\S+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RootUser();

    // COPY . .  /  COPY . /src  /  ADD . /app  (optionally with --chown etc.) - the whole context.
    [GeneratedRegex(@"^(?:--\S+\s+)*\.\s+\S+$")]
    private static partial Regex CopiesWholeContext();
}
