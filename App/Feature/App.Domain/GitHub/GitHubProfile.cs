namespace App.Domain.GitHub;

public sealed record GitHubProfile(long Id, string Login, int Followers, int Following);
