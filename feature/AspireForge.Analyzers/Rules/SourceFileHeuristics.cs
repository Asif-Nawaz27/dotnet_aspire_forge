using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules;

internal static class SourceFileHeuristics
{
    public static Task<bool> IsWebProjectAsync(ProjectContext context, CancellationToken cancellationToken) =>
        ContainsAnyAsync(context, cancellationToken, "WebApplication.CreateBuilder");

    public static async Task<bool> ContainsAnyAsync(
        ProjectContext context,
        CancellationToken cancellationToken,
        params string[] tokens)
    {
        foreach (var file in context.SourceFiles)
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);

            if (tokens.Any(token => content.Contains(token, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
