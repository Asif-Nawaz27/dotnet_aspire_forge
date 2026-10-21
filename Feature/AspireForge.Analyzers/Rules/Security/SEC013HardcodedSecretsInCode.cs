using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// SEC004's counterpart for C# source: secrets written as string literals. Once compiled, they're in
// every build artifact and container image - and in source control history forever, even after the
// line is deleted. Rotating the secret is the only fix once it has been committed.
//
// Flagged (first argument / value is a non-empty string literal):
//   - key material passed to crypto APIs: new SymmetricSecurityKey(Encoding.*.GetBytes("...")),
//     new SymmetricSecurityKey(Convert.FromBase64String("...")), new HMACSHA256/384/512(...("...")),
//     new NetworkCredential("user", "password")
//   - a string literal containing a connection-string credential: Password=, Pwd=, AccountKey=,
//     SharedAccessKey= with a literal value
//   - a literal assigned to a variable, field, property, or named argument whose name ends in
//     Password, Pwd, Secret, ApiKey, SigningKey, PrivateKey, AccessKey, or Token - including property
//     initializers (`public string ApiKey { get; set; } = "...";`)
//
// The name-based check skips values that look like something other than a secret: shorter than 8
// characters, containing whitespace or parentheses (messages, code snippets), a header name
// ("X-Api-Key"), a configuration path ("Jwt:SigningKey"), or a URL. The connection-string check skips
// strings whose server is the local machine (localhost, 127.0.0.1, ::1, ".", (localdb)) - a credential
// that only works on the developer's own machine isn't a leaked secret. The crypto-API check has no
// filter. Findings name the location, never the value. Development guards don't exempt anything here: a literal in source is committed regardless of
// which environment runs it.
public sealed partial class SEC013HardcodedSecretsInCode : IAnalysisRule
{
    private const int MaxFindingsInDescription = 3;
    private const int MinimumSecretLength = 8;

    public string Id => "SEC013";

    public string Title => "Hard-coded secrets in code";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<(string File, int Line, string What)>();

        foreach (var file in context.SourceFiles)
        {
            var code = await SourceFileHeuristics.ReadCodeAsync(file, cancellationToken);

            foreach (Match match in LiteralKeyMaterial().Matches(code))
            {
                findings.Add((file, SourceFileHeuristics.LineOf(code, match.Index), $"literal key in {match.Groups["api"].Value}"));
            }

            foreach (Match match in LiteralNetworkCredential().Matches(code))
            {
                findings.Add((file, SourceFileHeuristics.LineOf(code, match.Index), "literal password in NetworkCredential"));
            }

            foreach (Match match in ConnectionStringCredential().Matches(code))
            {
                if (!PointsAtLocalMachine(StringLiteralAt(code, match.Index)))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, match.Index),
                        $"{match.Groups["key"].Value}= in a connection string literal"));
                }
            }

            foreach (Match match in SensitiveNameAssignment().Matches(code))
            {
                if (LooksLikeASecret(match.Groups["value"].Value))
                {
                    findings.Add((file, SourceFileHeuristics.LineOf(code, match.Index),
                        $"literal assigned to '{match.Groups["name"].Value}'"));
                }
            }
        }

        if (findings.Count == 0)
        {
            return null;
        }

        // One finding per line: a connection string assigned to `dbPassword` would otherwise count twice.
        findings = findings
            .GroupBy(finding => (finding.File, finding.Line))
            .Select(group => group.First())
            .OrderBy(finding => finding.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Line)
            .ToList();

        var listed = string.Join(", ", findings.Take(MaxFindingsInDescription)
            .Select(finding => $"{finding.What} ({Path.GetFileName(finding.File)}:{finding.Line})"));
        var more = findings.Count > MaxFindingsInDescription
            ? $" (and {findings.Count - MaxFindingsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"Secrets are hard-coded in source: {listed}{more}. They ship in every build and stay in source control "
                + "history even after the line is removed - rotate them, then read them from configuration "
                + "(IConfiguration / IOptions) backed by user-secrets in development and environment variables or a "
                + "secret store such as Azure Key Vault in production.",
            DefaultSeverity,
            findings[0].File,
            findings[0].Line);
    }

    // Keys and tokens practically never contain whitespace or parentheses; search strings, messages,
    // and code snippets ("AddConsoleExporter(") do.
    private static bool LooksLikeASecret(string value) =>
        value.Length >= MinimumSecretLength
        && !value.Any(c => char.IsWhiteSpace(c) || c is '(' or ')')
        && !value.StartsWith("X-", StringComparison.OrdinalIgnoreCase)
        && !ConfigurationPath().IsMatch(value)
        && !Uri.IsWellFormedUriString(value, UriKind.Absolute);

    // A credential that only works against a database on the developer's own machine (a local default
    // or a generated template) isn't a leaked secret.
    private static bool PointsAtLocalMachine(string connectionString) =>
        LocalServer().IsMatch(connectionString);

    // The single-line literal starting at the opening quote `quoteIndex`.
    private static string StringLiteralAt(string code, int quoteIndex)
    {
        var end = code.IndexOfAny(['"', '\n'], quoteIndex + 1);
        return end < 0 ? code[quoteIndex..] : code[quoteIndex..end];
    }

    // new SymmetricSecurityKey(Encoding.UTF8.GetBytes("...")) / (Convert.FromBase64String("...")) and
    // the HMAC constructors, which take the key bytes the same way.
    [GeneratedRegex(@"\bnew\s+(?<api>SymmetricSecurityKey|HMACSHA256|HMACSHA384|HMACSHA512|HMACSHA1|HMACMD5)\s*\(\s*(?:(?:System\.Text\.)?Encoding\.\w+\.GetBytes|Convert\.FromBase64String)\s*\(\s*@?""[^""]+""")]
    private static partial Regex LiteralKeyMaterial();

    [GeneratedRegex(@"\bnew\s+NetworkCredential\s*\(\s*@?""[^""]*""\s*,\s*@?""[^""]+""")]
    private static partial Regex LiteralNetworkCredential();

    // Inside one string literal on one line. The value must be literal - not empty, not an
    // interpolation hole ({password}), and not an obvious placeholder (<password>, ***).
    [GeneratedRegex(@"""[^""\r\n]*?\b(?<key>Password|Pwd|AccountKey|SharedAccessKey)\s*=\s*(?![;""{<*\s])[^;""\r\n]+", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringCredential();

    // `name = "value"` (assignment, initializer), `name: "value"` (named argument), or
    // `name { get; set; } = "value"`. Not `==`, not `=>`, not `::`.
    [GeneratedRegex(@"\b(?<name>\w*(?:Password|Pwd|Secret|ApiKey|SigningKey|PrivateKey|AccessKey|Token))\s*(?:\{[^{}]*\}\s*)?(?:=(?![=>])|:(?!:))\s*@?""(?<value>[^""\r\n]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveNameAssignment();

    [GeneratedRegex(@"^[\w.-]+(?::[\w.-]+)+$")]
    private static partial Regex ConfigurationPath();

    [GeneratedRegex(@"\b(?:Host|Server|Data\s+Source|Address|Addr)\s*=\s*(?:tcp:)?(?:localhost|127\.0\.0\.1|::1|\(localdb\)|\.)(?=[;:,\\\s""]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex LocalServer();
}
