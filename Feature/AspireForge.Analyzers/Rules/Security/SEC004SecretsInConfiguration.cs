using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AspireForge.Core.Analysis;

namespace AspireForge.Analyzers.Rules.Security;

// Flags literal secrets in appsettings*.json. These files are copied into every `dotnet publish`
// output and container image, so a secret in one leaks to anyone with the artifact - whether or not
// the file is also committed to source control.
//
// appsettings.Development.json is deliberately skipped: a local database password there is normal
// and never reaches production. Every other environment file (base, Production, Staging, ...) is
// scanned. Reported findings name the key and file only - never the secret value itself.
public sealed partial class SEC004SecretsInConfiguration : IAnalysisRule
{
    private const int MaxFindingsInDescription = 5;

    // Matched against the END of a key name, case-insensitively, so "Jwt:SigningKey" and
    // "GitHub:AccessToken" match but "TokenLifetime" or "PasswordPolicy" sections don't.
    private static readonly string[] SensitiveKeySuffixes =
    [
        "password",
        "pwd",
        "secret",
        "apikey",
        "api_key",
        "signingkey",
        "privatekey",
        "accesskey",
        "token",
    ];

    public string Id => "SEC004";

    public string Title => "Secrets in configuration files";

    public Severity DefaultSeverity => Severity.Error;

    public async Task<AnalysisIssue?> EvaluateAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();

        foreach (var file in EnumerateConfigurationFiles(context.RootDirectory))
        {
            var content = await File.ReadAllBytesAsync(file, cancellationToken);
            findings.AddRange(Scan(file, content));
        }

        if (findings.Count == 0)
        {
            return null;
        }

        var first = findings[0];
        var listed = string.Join(", ", findings.Take(MaxFindingsInDescription).Select(finding => finding.Describe()));
        var more = findings.Count > MaxFindingsInDescription
            ? $" (and {findings.Count - MaxFindingsInDescription} more)"
            : string.Empty;

        return new AnalysisIssue(
            Id,
            Title,
            $"Configuration files contain literal secrets: {listed}{more}. These files ship inside every "
                + "publish output and container image. Leave the values empty and supply them from user-secrets "
                + "(development), environment variables, or a secret store such as Azure Key Vault.",
            DefaultSeverity,
            first.File,
            first.Line);
    }

    private static IEnumerable<string> EnumerateConfigurationFiles(string rootDirectory) =>
        Directory.Exists(rootDirectory)
            ? Directory.EnumerateFiles(rootDirectory, "appsettings*.json", SearchOption.TopDirectoryOnly)
                .Where(file => !Path.GetFileName(file).Equals("appsettings.Development.json", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
            : [];

    // Walks the JSON with a reader rather than a DOM so each finding keeps its line number.
    private static List<Finding> Scan(string file, byte[] content)
    {
        var findings = new List<Finding>();
        var json = StripUtf8Bom(content);

        var reader = new Utf8JsonReader(json, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var path = new List<string>();
        string? propertyName = null;

        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        propertyName = reader.GetString();
                        break;

                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        path.Add(propertyName ?? string.Empty);
                        propertyName = null;
                        break;

                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        path.RemoveAt(path.Count - 1);
                        propertyName = null;
                        break;

                    case JsonTokenType.String:
                        // Array items have no name of their own; judge them by their containing key.
                        var key = propertyName ?? path.LastOrDefault() ?? string.Empty;
                        var reason = Classify(key, reader.GetString() ?? string.Empty);

                        if (reason is not null)
                        {
                            findings.Add(new Finding(
                                file,
                                LineOf(json, reader.TokenStartIndex),
                                FormatPath(path, propertyName),
                                reason));
                        }

                        propertyName = null;
                        break;

                    default:
                        propertyName = null;
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed JSON fails the app at startup anyway - not this rule's concern. Keep whatever
            // was found before the error.
        }

        return findings;
    }

    private static string? Classify(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || IsSecretReference(value))
        {
            return null;
        }

        if (ConnectionStringSecretPattern().IsMatch(value))
        {
            return "credential in connection string";
        }

        return SensitiveKeySuffixes.Any(suffix => key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ? "literal value"
            : null;
    }

    // Values that point at a secret store rather than containing the secret: Azure App Service Key
    // Vault references, and ${VAR}-style placeholders resolved by a deployment tool.
    private static bool IsSecretReference(string value) =>
        value.StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase)
        || (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}'));

    private static ReadOnlySpan<byte> StripUtf8Bom(byte[] content) =>
        content.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? content.AsSpan(Encoding.UTF8.Preamble.Length) : content;

    private static int LineOf(ReadOnlySpan<byte> json, long index) =>
        json[..(int)index].Count((byte)'\n') + 1;

    private static string FormatPath(List<string> path, string? propertyName) =>
        string.Join(':', path.Append(propertyName ?? string.Empty).Where(segment => segment.Length > 0));

    // Password/Pwd (ADO.NET, Npgsql, SQL Server), AccountKey (Azure Storage), SharedAccessKey (Service
    // Bus, Event Hubs) with a non-empty value.
    [GeneratedRegex(@"(?i)(?:^|;)\s*(?:password|pwd|accountkey|sharedaccesskey)\s*=\s*[^;\s]")]
    private static partial Regex ConnectionStringSecretPattern();

    private sealed record Finding(string File, int Line, string Path, string Reason)
    {
        public string Describe() => $"'{Path}' ({Reason}) in {System.IO.Path.GetFileName(File)}:{Line}";
    }
}
