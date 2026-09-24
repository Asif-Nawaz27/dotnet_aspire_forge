namespace App.Domain;

public sealed record GitHubUser(long Id, string Login, string AvatarUrl, string HtmlUrl);
