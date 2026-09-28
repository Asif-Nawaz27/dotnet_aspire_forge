using App.Domain.GitHub;

namespace App.Application.GitHub;

public interface IGitHubUserService
{
    // Accounts this user follows that don't follow it back.
    Task<IReadOnlyList<GitHubUser>> GetNotFollowingBackAsync(
        string username, CancellationToken cancellationToken = default);

    // Both directions of the relationship, plus totals.
    Task<FollowRelationshipReport> GetRelationshipReportAsync(
        string username, CancellationToken cancellationToken = default);

    // Newest first.
    Task<IReadOnlyList<FollowSnapshot>> GetHistoryAsync(
        string username, int limit, CancellationToken cancellationToken = default);
}
