using App.Domain.GitHub;

namespace App.Application.Abstractions;

// Port for the GitHub REST API. Implementations handle transport concerns (auth, pagination, rate
// limits) and throw NotFoundException / UpstreamServiceException for expected failures.
public interface IGitHubClient
{
    // A single request, including follower/following counts - cheap enough to check before paging.
    Task<GitHubProfile> GetProfileAsync(string username, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubUser>> GetFollowersAsync(string username, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubUser>> GetFollowingAsync(string username, CancellationToken cancellationToken = default);
}
