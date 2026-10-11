using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// SEC001 checks that callers CAN be identified; this checks that anything actually REQUIRES it.
// Authentication alone only reads the token - an endpoint without an authorization requirement still
// serves anonymous requests. So a project can register JWT bearer, pass SEC001, and leave every
// endpoint public without any visible symptom.
//
// Recognised ways of requiring authorization, anywhere in the project's source (including referenced
// projects):
//   - the [Authorize] attribute in any form: [Authorize], [Authorize(Roles = "...")],
//     [ApiController, Authorize], [Microsoft.AspNetCore.Authorization.Authorize]
//   - RequireAuthorization() on an endpoint, a route group, or MapControllers()
//   - a fallback policy (options.FallbackPolicy = ..., or SetFallbackPolicy(...)), which protects
//     every endpoint that doesn't opt out with [AllowAnonymous]
//   - a global AuthorizeFilter / new AuthorizeAttribute() added to MVC filters or conventions
//   - Razor Pages conventions: AuthorizeFolder, AuthorizePage, AuthorizeAreaFolder, AuthorizeAreaPage
// It stays silent when authentication isn't configured at all - that's SEC001's finding.
public sealed partial class SEC008AuthorizationNotEnforced : IAnalysisRule
{
    private static readonly string[] EnforcementTokens =
    [
        "RequireAuthorization(",
        "FallbackPolicy",
        "AuthorizeFilter",
        "AuthorizeAttribute(",
        "AuthorizeFolder(",
        "AuthorizePage(",
        "AuthorizeAreaFolder(",
        "AuthorizeAreaPage(",
    ];

    public string Id => "SEC008";

    public string Title => "Authorization not enforced";

    public Severity DefaultSeverity => Severity.Warning;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await SourceFileHeuristics.IsWebProjectAsync(context, cancellationToken)
            || !await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, "AddAuthentication"))
        {
            return null;
        }

        if (await SourceFileHeuristics.ContainsAnyAsync(context, cancellationToken, EnforcementTokens)
            || await SourceFileHeuristics.MatchesAsync(context, cancellationToken, AuthorizeAttribute()))
        {
            return null;
        }

        return new AnalysisIssue(
            Id,
            Title,
            "Authentication is configured, but no endpoint requires it: no [Authorize] attribute, RequireAuthorization() "
                + "call, or authorization fallback policy was found. Every endpoint accepts anonymous requests. Add "
                + "[Authorize] or .RequireAuthorization() where callers must be identified, or set a fallback policy "
                + "(options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()) to "
                + "protect everything by default and opt out with [AllowAnonymous].",
            DefaultSeverity,
            context.ProjectFile);
    }

    // [Authorize], [Authorize(...)], [X, Authorize], [Authorize, X], [Namespace.Authorize],
    // [AuthorizeAttribute] - but not [AuthorizeView] or other names that merely start with "Authorize".
    [GeneratedRegex(@"\[(?:[^\[\]]*,\s*)?(?:[\w.]+\.)?Authorize(?:Attribute)?\s*[\](,]")]
    private static partial Regex AuthorizeAttribute();
}
