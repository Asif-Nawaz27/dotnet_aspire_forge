using App.Application.Abstractions;
using App.Domain.GitHub;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistence;

internal sealed class FollowSnapshotRepository(AppDbContext dbContext) : IFollowSnapshotRepository
{
    public async Task AddRangeAsync(
        IReadOnlyCollection<FollowSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        dbContext.FollowSnapshots.AddRange(snapshots);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FollowSnapshot>> GetHistoryAsync(
        string username, int limit, CancellationToken cancellationToken = default)
    {
        var normalized = username.ToLowerInvariant();

        return await dbContext.FollowSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.Username == normalized)
            .OrderByDescending(snapshot => snapshot.CapturedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
