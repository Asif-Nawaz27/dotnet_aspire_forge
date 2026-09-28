namespace App.Domain.GitHub;

public sealed record GitHubUser(long Id, string Login, string AvatarUrl, string HtmlUrl);
