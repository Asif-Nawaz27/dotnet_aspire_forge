using System.Globalization;
using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// Flags JWT bearer settings that switch off a check the token handler performs by default. These are
// typically added to get past an error during development ("IDX10223: Lifetime validation failed")
// and then ship, quietly turning token validation into token parsing.
//
// Errors - the token's authenticity or expiry is no longer verified:
//   RequireSignedTokens = false            unsigned ("alg": "none") tokens are accepted
//   SignatureValidator = ... new JwtSecurityToken(token) / new JsonWebToken(token)
//                                          the signature is never checked - anyone can mint a token
//   ValidateLifetime = false               expired tokens are accepted forever
// Warnings - tokens meant for someone else, or kept alive too long, are accepted:
//   ValidateIssuer = false                 tokens from any issuer that shares the key
//   ValidateAudience = false               tokens minted for a different API
//   RequireExpirationTime = false          tokens with no expiry at all
//   RequireHttpsMetadata = false           signing keys fetched over plain HTTP can be swapped in transit
//   ClockSkew above 5 minutes              every token outlives its expiry by that much
//
// ValidateIssuerSigningKey = false is deliberately NOT flagged: it's the default, and it doesn't
// disable signature validation - it only skips extra checks on the key itself (e.g. certificate expiry).
// Settings inside a development-only guard (`if (env.IsDevelopment())`, `#if DEBUG`) are allowed.
public sealed partial class SEC005WeakJwtValidation : IAnalysisRule
{
    private const int MaxFindingsInDescription = 4;

    // The default ClockSkew in Microsoft.IdentityModel.
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<string, (Severity Severity, string Consequence)> DisabledChecks = new()
    {
        ["RequireSignedTokens"] = (Severity.Error, "unsigned (\"alg\": \"none\") tokens are accepted"),
        ["ValidateLifetime"] = (Severity.Error, "expired tokens are accepted forever"),
        ["ValidateIssuer"] = (Severity.Warning, "tokens from any issuer that shares the signing key are accepted"),
        ["ValidateAudience"] = (Severity.Warning, "tokens minted for a different API are accepted"),
        ["RequireExpirationTime"] = (Severity.Warning, "tokens with no expiry are accepted and never expire"),
        ["RequireHttpsMetadata"] = (Severity.Warning, "signing keys are fetched over plain HTTP and can be replaced in transit"),
    };

    public string Id => "SEC005";

    public string Title => "Weak JWT validation";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match match in DisabledCheck().Matches(code))
            {
                var setting = match.Groups["name"].Value;
                var (severity, consequence) = DisabledChecks[setting];
                Add(findings, file, code, match.Index, severity, $"{setting} = false - {consequence}");
            }

            foreach (Match match in SignatureValidatorBypass().Matches(code))
            {
                Add(findings, file, code, match.Index, Severity.Error,
                    "SignatureValidator returns the token without verifying it - anyone can mint a valid token");
            }

            foreach (Match match in ClockSkewAssignment().Matches(code))
            {
                if (ParseTimeSpan(match) is { } skew && skew > MaxClockSkew)
                {
                    Add(findings, file, code, match.Index, Severity.Warning,
                        $"ClockSkew = {match.Groups["value"].Value.Trim()} - every token stays valid that long past its expiry (default is 5 minutes)");
                }
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        findings = findings
            .OrderByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Line)
            .ToList();

        var listed = string.Join("; ", findings.Take(MaxFindingsInDescription).Select(finding => finding.Describe()));
        var more = findings.Count > MaxFindingsInDescription
            ? $" (and {findings.Count - MaxFindingsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"JWT validation is weakened: {listed}{more}. Keep the token handler's defaults, or limit relaxed "
                + "settings to development with an IsDevelopment() check.",
            findings[0].Severity,
            findings[0].File,
            findings[0].Line);
    }

    private static void Add(List<Finding> findings, string file, string code, int index, Severity severity, string message)
    {
        if (!DevelopmentGuard.Covers(code, index))
        {
            findings.Add(new Finding(file, SourceFileHeuristics.LineOf(code, index), severity, message));
        }
    }

    // TimeSpan.MaxValue, TimeSpan.FromDays/Hours/Minutes/Seconds(n), or new TimeSpan(h, m, s).
    private static TimeSpan? ParseTimeSpan(Match match)
    {
        var value = match.Groups["value"].Value;

        if (value.Contains("MaxValue", StringComparison.Ordinal))
        {
            return TimeSpan.MaxValue;
        }

        var factory = TimeSpanFactory().Match(value);
        if (factory.Success && double.TryParse(factory.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
        {
            return factory.Groups["unit"].Value switch
            {
                "Days" => TimeSpan.FromDays(n),
                "Hours" => TimeSpan.FromHours(n),
                "Minutes" => TimeSpan.FromMinutes(n),
                "Seconds" => TimeSpan.FromSeconds(n),
                _ => null,
            };
        }

        var constructor = TimeSpanConstructor().Match(value);
        if (constructor.Success)
        {
            return new TimeSpan(
                int.Parse(constructor.Groups["h"].Value, CultureInfo.InvariantCulture),
                int.Parse(constructor.Groups["m"].Value, CultureInfo.InvariantCulture),
                int.Parse(constructor.Groups["s"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    private sealed record Finding(string File, int Line, Severity Severity, string Message)
    {
        public string Describe() => $"{Message} ({Path.GetFileName(File)}:{Line})";
    }

    [GeneratedRegex(@"\b(?<name>RequireSignedTokens|ValidateLifetime|ValidateIssuer|ValidateAudience|RequireExpirationTime|RequireHttpsMetadata)\s*=(?!=)\s*false\b")]
    private static partial Regex DisabledCheck();

    // A SignatureValidator that just wraps the raw token - the well-known "make the error go away" bypass.
    [GeneratedRegex(@"\bSignatureValidator\s*=(?!=)[^;]{0,200}?\bnew\s+(?:JwtSecurityToken|JsonWebToken)\s*\(", RegexOptions.Singleline)]
    private static partial Regex SignatureValidatorBypass();

    // The value runs to the next top-level ',' / ';' / '}' - commas inside "(...)" belong to the value.
    [GeneratedRegex(@"\bClockSkew\s*=(?!=)\s*(?<value>(?:[^,;\r\n}()]|\([^()]*\))+)")]
    private static partial Regex ClockSkewAssignment();

    [GeneratedRegex(@"TimeSpan\.From(?<unit>Days|Hours|Minutes|Seconds)\s*\(\s*(?<n>\d+(?:\.\d+)?)\s*\)")]
    private static partial Regex TimeSpanFactory();

    [GeneratedRegex(@"new\s+TimeSpan\s*\(\s*(?<h>\d+)\s*,\s*(?<m>\d+)\s*,\s*(?<s>\d+)\s*\)")]
    private static partial Regex TimeSpanConstructor();
}
