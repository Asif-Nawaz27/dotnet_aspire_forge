using App.Domain.GitHub;

namespace App.Application.Abstractions;

public interface IFollowSnapshotRepository
{
    Task AddRangeAsync(IReadOnlyCollection<FollowSnapshot> snapshots, CancellationToken cancellationToken = default);

    // Newest first.
    Task<IReadOnlyList<FollowSnapshot>> GetHistoryAsync(
        string username, int limit, CancellationToken cancellationToken = default);
}
