using App.Domain;

namespace App.Application;

public interface IGitHubUserService
{
    // Followers who don't appear in the following list - people following this account that it
    // doesn't follow back.
    Task<IReadOnlyList<GitHubUser>> GetFollowersNotFollowingBackAsync(
        string username, CancellationToken cancellationToken = default);
}
