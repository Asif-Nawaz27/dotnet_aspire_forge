using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// Middleware meant for a developer's machine, registered where it also runs in production:
//
//   error    UseDeveloperExceptionPage - every unhandled exception returns the stack trace, source
//            snippets, headers, and cookies to whoever sent the request
//   error    UseMigrationsEndPoint / UseDatabaseErrorPage - EF Core's development endpoint, which can
//            apply pending migrations to the database over HTTP
//   warning  API documentation - MapOpenApi, UseSwagger, UseSwaggerUI, MapScalarApiReference, UseReDoc,
//            NSwag's UseOpenApi / UseSwaggerUi - publishes a map of every endpoint and model. Sometimes
//            deliberate for a public API, so a warning rather than an error.
//
// A call is fine inside a development guard (`if (app.Environment.IsDevelopment())`, the `else` of
// `if (!...IsDevelopment())`, `#if DEBUG`) - or, because this rule's concern is production
// specifically, inside a non-production guard (`if (!app.Environment.IsProduction())`), the common way
// to keep API docs on in staging. Only checked in web projects.
public sealed partial class SEC007DevelopmentFeaturesInProduction : IAnalysisRule
{
    private static readonly Dictionary<string, (Severity Severity, string Consequence)> Features = new()
    {
        ["UseDeveloperExceptionPage"] = (Severity.Error, "returns stack traces and request details to anyone"),
        ["UseMigrationsEndPoint"] = (Severity.Error, "lets HTTP requests apply database migrations"),
        ["UseDatabaseErrorPage"] = (Severity.Error, "exposes database errors and migration actions"),
        ["MapOpenApi"] = (Severity.Warning, "publishes the API's endpoints and models"),
        ["UseSwagger"] = (Severity.Warning, "publishes the API's endpoints and models"),
        ["UseSwaggerUI"] = (Severity.Warning, "publishes an interactive console for every endpoint"),
        ["UseSwaggerUi"] = (Severity.Warning, "publishes an interactive console for every endpoint"),
        ["UseOpenApi"] = (Severity.Warning, "publishes the API's endpoints and models"),
        ["MapScalarApiReference"] = (Severity.Warning, "publishes an interactive console for every endpoint"),
        ["UseReDoc"] = (Severity.Warning, "publishes the API's endpoints and models"),
    };

    public string Id => "SEC007";

    public string Title => "Development features enabled in production";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        var findings = new List<(string File, int Line, string Feature, Severity Severity, string Consequence)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match call in FeatureCall().Matches(code))
            {
                if (DevelopmentGuard.Covers(code, call.Index, allowNonProduction: true))
                {
                    continue;
                }

                var feature = call.Groups["feature"].Value;
                var (severity, consequence) = Features[feature];
                findings.Add((file, SourceFileHeuristics.LineOf(code, call.Index), feature, severity, consequence));
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Line)
            .ToList();

        var listed = string.Join("; ", findings.Select(f => $"{f.Feature}() {f.Consequence} ({Path.GetFileName(f.File)}:{f.Line})"));

        return new AnalysisIssue(
            Id,
            Title,
            $"Development-only features run in every environment: {listed}. Wrap them in "
                + "if (app.Environment.IsDevelopment()) - and use UseExceptionHandler() for production error responses.",
            findings[0].Severity,
            findings[0].File,
            findings[0].Line);
    }

    [GeneratedRegex(@"\.(?<feature>UseDeveloperExceptionPage|UseMigrationsEndPoint|UseDatabaseErrorPage|MapOpenApi|UseSwaggerUI|UseSwaggerUi|UseSwagger|UseOpenApi|MapScalarApiReference|UseReDoc)\s*(?:<[^<>()]*>)?\s*\(")]
    private static partial Regex FeatureCall();
}
