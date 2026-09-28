namespace App.Domain.GitHub;

// Both directions of a follow relationship, computed from a single fetch of the followers and
// following lists so neither view costs extra GitHub API calls.
public sealed record FollowRelationshipReport(
    string Username,
    int FollowersCount,
    int FollowingCount,
    IReadOnlyList<GitHubUser> NotFollowingBack,
    IReadOnlyList<GitHubUser> NotFollowedBack)
{
    public static FollowRelationshipReport Create(
        string username, IReadOnlyCollection<GitHubUser> followers, IReadOnlyCollection<GitHubUser> following)
    {
        var followerLogins = followers.Select(user => user.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var followingLogins = following.Select(user => user.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new FollowRelationshipReport(
            username,
            followers.Count,
            following.Count,
            // Accounts this user follows that don't follow it back.
            following.Where(user => !followerLogins.Contains(user.Login)).ToList(),
            // Accounts following this user that it doesn't follow back.
            followers.Where(user => !followingLogins.Contains(user.Login)).ToList());
    }
}
