using App.Application.Abstractions;
using App.Domain.Exceptions;
using App.Domain.GitHub;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Application.GitHub;

public sealed partial class GitHubUserService(
    IGitHubClient gitHubClient,
    IFollowSnapshotRepository snapshotRepository,
    IFollowSnapshotQueue snapshotQueue,
    HybridCache cache,
    TimeProvider timeProvider,
    IOptions<GitHubUserServiceOptions> options,
    ILogger<GitHubUserService> logger) : IGitHubUserService
{
    public async Task<IReadOnlyList<GitHubUser>> GetNotFollowingBackAsync(
        string username, CancellationToken cancellationToken = default)
    {
        var report = await GetRelationshipReportAsync(username, cancellationToken);
        return report.NotFollowingBack;
    }

    public async Task<FollowRelationshipReport> GetRelationshipReportAsync(
        string username, CancellationToken cancellationToken = default)
    {
        EnsureValid(username);

        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = options.Value.CacheDuration,
            LocalCacheExpiration = options.Value.CacheDuration,
        };

        return await cache.GetOrCreateAsync(
            $"github:relationship:{username.ToLowerInvariant()}",
            (Service: this, Username: username),
            static (state, token) => state.Service.BuildReportAsync(state.Username, token),
            entryOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<FollowSnapshot>> GetHistoryAsync(
        string username, int limit, CancellationToken cancellationToken = default)
    {
        EnsureValid(username);

        if (limit < 1 || limit > options.Value.MaxHistoryItems)
        {
            throw new ValidationException(
                nameof(limit), $"Limit must be between 1 and {options.Value.MaxHistoryItems}.");
        }

        return await snapshotRepository.GetHistoryAsync(username, limit, cancellationToken);
    }

    // Only runs on a cache miss, so a snapshot is recorded once per fresh fetch rather than per request.
    private async ValueTask<FollowRelationshipReport> BuildReportAsync(
        string username, CancellationToken cancellationToken)
    {
        // One request up front, so unknown and oversized accounts fail before any paging happens.
        var profile = await gitHubClient.GetProfileAsync(username, cancellationToken);
        var maxSize = options.Value.MaxRelationshipSize;

        if (profile.Followers > maxSize || profile.Following > maxSize)
        {
            throw new ValidationException(
                nameof(username),
                $"'{username}' has {profile.Followers} followers and follows {profile.Following}; accounts above {maxSize} in either list are not supported.");
        }

        // Independent calls - fetch both lists concurrently.
        var followersTask = gitHubClient.GetFollowersAsync(username, cancellationToken);
        var followingTask = gitHubClient.GetFollowingAsync(username, cancellationToken);
        await Task.WhenAll(followersTask, followingTask);

        var report = FollowRelationshipReport.Create(username, await followersTask, await followingTask);

        LogReportBuilt(username, report.FollowersCount, report.FollowingCount, report.NotFollowingBack.Count);

        // History is a secondary feature: persisted in the background, and dropped rather than blocking
        // the lookup if the writer has fallen too far behind.
        if (!snapshotQueue.TryEnqueue(FollowSnapshot.From(report, timeProvider.GetUtcNow())))
        {
            LogSnapshotDropped(username);
        }

        return report;
    }

    private static void EnsureValid(string username)
    {
        if (!GitHubUsername.IsValid(username))
        {
            throw new ValidationException(nameof(username), $"'{username}' is not a valid GitHub username.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Built follow report for {Username}: {FollowersCount} followers, {FollowingCount} following, {NotFollowingBackCount} not following back")]
    private partial void LogReportBuilt(string username, int followersCount, int followingCount, int notFollowingBackCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Snapshot queue is full; dropped follow snapshot for {Username}")]
    private partial void LogSnapshotDropped(string username);
}
