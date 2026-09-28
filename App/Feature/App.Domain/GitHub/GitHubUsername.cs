using System.Text.RegularExpressions;

namespace App.Domain.GitHub;

public static partial class GitHubUsername
{
    // GitHub's own rules: 1-39 alphanumerics or single hyphens, not starting or ending with a hyphen.
    public const string Pattern = "^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$";

    public static bool IsValid(string? username) => username is not null && PatternRegex().IsMatch(username);

    [GeneratedRegex(Pattern)]
    private static partial Regex PatternRegex();
}
