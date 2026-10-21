using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

// An `async void` method has no Task for a caller to await, so an exception thrown after its first
// await has nowhere to go: it's raised on the thread pool, where it's unhandled - and an unhandled
// exception terminates the process. In a web app that's every in-flight request, not just the one that
// failed. The caller also can't tell when the work finishes, so the request (and its scoped services,
// such as a DbContext) can be disposed while the method is still using them.
//
// Flags every `async void` method declaration - including local functions - except event handlers,
// recognised by the (object sender, ...EventArgs e) signature, where the delegate type forces void.
// Not flagged: async lambdas (their delegate type isn't visible to a text heuristic) and comments.
public sealed partial class REL008AsyncVoidMethod : IAnalysisRule
{
    private const int MaxLocationsInDescription = 3;

    public string Id => "REL008";

    public string Title => "async void method";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line, string Method)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match match in AsyncVoidDeclaration().Matches(code))
            {
                if (!EventHandlerParameters().IsMatch(match.Groups["parameters"].Value))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, match.Index), match.Groups["name"].Value));
                }
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        var locations = string.Join(
            ", ",
            findings.Take(MaxLocationsInDescription).Select(f => $"{f.Method}() at {Path.GetFileName(f.File)}:{f.Line}"));
        var more = findings.Count > MaxLocationsInDescription
            ? $" (and {findings.Count - MaxLocationsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"async void methods found: {locations}{more}. An exception after the first await can't be caught by the "
                + "caller and crashes the whole process, and the caller can't wait for the work to finish. Return Task "
                + "(or ValueTask) and await it; for fire-and-forget work, use a BackgroundService or a queue with its "
                + "own error handling.",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    // `async void Name(...)` or `async void Name<T>(...)`, with any modifiers before it.
    [GeneratedRegex(@"\basync\s+void\s+(?<name>\w+)\s*(?:<[^<>()]*>)?\s*\((?<parameters>[^()]*)\)")]
    private static partial Regex AsyncVoidDeclaration();

    // (object sender, EventArgs e), (object? sender, RoutedEventArgs e), (object s, System.Timers.ElapsedEventArgs args)
    [GeneratedRegex(@"^\s*object\??\s+\w+\s*,\s*[\w.]*EventArgs\??\s+\w+\s*$")]
    private static partial Regex EventHandlerParameters();
}
