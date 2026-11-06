using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// HTTPS redirection (SEC003) upgrades a request after it has already been sent over plain HTTP - and
// that first request is the one an attacker on the network (public Wi-Fi, a compromised router) can
// intercept or rewrite: stripping the redirect, stealing cookies, or serving a fake page. HSTS
// (Strict-Transport-Security) tells the browser to go straight to HTTPS for every later visit, closing
// that window. UseHsts() is what sends the header.
//
// Fires for web projects when:
//   - UseHsts() is never called, or
//   - every call is inside a development-only guard (`if (env.IsDevelopment())`, `#if DEBUG`) - the
//     header is then sent only where it doesn't matter, and never in production.
// The ASP.NET Core template shapes pass: UseHsts() in the `else` of `if (app.Environment.IsDevelopment())`,
// or inside `if (!app.Environment.IsDevelopment())`.
public sealed partial class SEC006HstsMissing : IAnalysisRule
{
    public string Id => "SEC006";

    public string Title => "HSTS missing";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken))
        {
            return null;
        }

        (string File, int Line)? developmentOnly = null;

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match call in UseHstsCall().Matches(code))
            {
                if (!DevelopmentGuard.Covers(code, call.Index))
                {
                    return null; // runs outside development - the header reaches production
                }

                developmentOnly ??= (file, SourceFileHeuristics.LineOf(code, call.Index));
            }
        }

        if (developmentOnly is { } location)
        {
            if (context.Suppressions.IsSuppressed(Id, location.File, location.Line))
            {
                return null;
            }

            return new AnalysisIssue(
                Id,
                Title,
                $"UseHsts() is only called inside a development-only block ({Path.GetFileName(location.File)}:{location.Line}), "
                    + "so production never sends the Strict-Transport-Security header - the opposite of what it's for. Call it "
                    + "outside development: in the else branch of if (app.Environment.IsDevelopment()).",
                DefaultSeverity,
                location.File,
                location.Line);
        }

        return new AnalysisIssue(
            Id,
            Title,
            "No call to UseHsts was found, so browsers aren't told to always use HTTPS. Every first visit over plain HTTP "
                + "can be intercepted before UseHttpsRedirection's redirect arrives. Add app.UseHsts() for non-development "
                + "environments.",
            DefaultSeverity,
            context.ProjectFile);
    }

    [GeneratedRegex(@"\.UseHsts\s*\(")]
    private static partial Regex UseHstsCall();
}
