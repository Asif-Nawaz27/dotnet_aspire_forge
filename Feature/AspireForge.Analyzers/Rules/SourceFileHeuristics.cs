using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules;

internal static class SourceFileHeuristics
{
    public static Task<bool> IsWebProjectAsync(ProjectContext context, CancellationToken cancellationToken) =>
        ContainsAnyAsync(context, cancellationToken, "WebApplication.CreateBuilder");

    public static Task<bool> ContainsAnyAsync(
        ProjectContext context,
        CancellationToken cancellationToken,
        params string[] tokens) =>
        ContainsAnyAsync(context.SourceFiles, cancellationToken, tokens);

    public static async Task<bool> ContainsAnyAsync(
        IEnumerable<string> files,
        CancellationToken cancellationToken,
        params string[] tokens)
    {
        foreach (var file in files)
        {
            var content = StripComments(await File.ReadAllTextAsync(file, cancellationToken));

            if (tokens.Any(token => content.Contains(token, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    // Source files belonging to this project itself, excluding the referenced projects' files that
    // SourceFiles also carries (see ProjectContextBuilder) - for rules like ARCH001 that care whether
    // THIS project's own code uses something, not whether it's merely reachable through a reference.
    public static IEnumerable<string> OwnSourceFiles(ProjectContext context) =>
        context.SourceFiles.Where(file =>
            file.StartsWith(context.RootDirectory, StringComparison.OrdinalIgnoreCase));

    // Strips comments before matching, so a token mentioned in a // note or a /* */ block - dead
    // text, not code - doesn't count as real usage. This is a plain regex pass, not a C# tokenizer:
    // it doesn't understand string literals, so "://" inside a URL string (e.g. an Authority setting)
    // would otherwise look like a line comment. The negative lookbehind for ':' before '//' covers
    // that specific, common case without needing full parsing.
    private static string StripComments(string content)
    {
        content = Regex.Replace(content, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        content = Regex.Replace(content, @"(?<!:)//[^\n]*", string.Empty);
        return content;
    }
}
