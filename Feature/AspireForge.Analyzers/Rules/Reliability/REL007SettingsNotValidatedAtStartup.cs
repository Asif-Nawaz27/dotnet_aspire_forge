using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Reliability;

// Bad configuration should stop the app at startup, not on whichever request first reads the value.
//
// Two shapes are checked:
//  - services.Configure<T>(section): returns IServiceCollection, so ValidateOnStart can never be
//    chained onto it - always flagged.
//  - AddOptions<T>().Bind(...) / .BindConfiguration(...): flagged when there are more bindings than
//    ValidateOnStart calls in the project's source.
public sealed partial class REL007SettingsNotValidatedAtStartup : IAnalysisRule
{
    public string Id => "REL007";

    public string Title => "Settings not validated at startup";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var configureBindings = await SourceFileHeuristics.CountMatchesAsync(context, cancellationToken, ConfigureFromSection());
        var optionsBindings = await SourceFileHeuristics.CountMatchesAsync(context, cancellationToken, OptionsBuilderBind());
        var validations = await SourceFileHeuristics.CountMatchesAsync(context, cancellationToken, ValidateOnStartCall());

        if (configureBindings == 0 && optionsBindings <= validations)
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "Configuration is bound to options without ValidateOnStart. Bad or missing settings will only fail on first use, in the middle of a request - "
                + "use AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart() so the app fails at startup instead.",
            DefaultSeverity,
            context.ProjectFile);
    }

    // [^;] keeps the match inside one statement.
    [GeneratedRegex(@"\bConfigure<[\w.]+>\s*\([^;]*?GetSection\s*\(")]
    private static partial Regex ConfigureFromSection();

    [GeneratedRegex(@"AddOptions<[\w.]+>\s*\([^)]*\)\s*\.Bind(Configuration)?\s*\(")]
    private static partial Regex OptionsBuilderBind();

    [GeneratedRegex(@"\.ValidateOnStart\s*\(")]
    private static partial Regex ValidateOnStartCall();
}
