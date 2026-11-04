using System.Text.RegularExpressions;

namespace AspireForge.Cli.Output;

// Links a rule to its section in docs/rules/<category>.md on GitHub. The anchor is computed the way
// GitHub slugs a "## <Id> - <Title>" heading, so a renamed rule or heading would break the link -
// RuleDocsTests checks every catalog entry against the real docs to catch that.
public static partial class RuleDocs
{
    public const string RepositoryUrl = "https://github.com/Asif-Nawaz27/dotnet_aspire_forge";

    public static string Url(string category, string ruleId, string ruleTitle) =>
        $"{RepositoryUrl}/blob/main/docs/rules/{category.ToLowerInvariant()}.md#{Anchor($"{ruleId} - {ruleTitle}")}";

    // GitHub heading slugs: lower-case, drop everything except letters, digits, spaces, hyphens and
    // underscores, then turn each space into a hyphen ("SEC006 - HSTS missing" -> "sec006---hsts-missing").
    public static string Anchor(string heading) =>
        NonSlugCharacters().Replace(heading.Trim().ToLowerInvariant(), string.Empty).Replace(' ', '-');

    [GeneratedRegex(@"[^\p{L}\p{N} _-]")]
    private static partial Regex NonSlugCharacters();
}
