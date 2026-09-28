using App.Application.Abstractions;
using App.Application.GitHub;
using App.Domain.Exceptions;
using App.Domain.GitHub;
using App.UnitTests.Domain;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace App.UnitTests.Application;

public class GitHubUserServiceTests
{
    private readonly FakeGitHubClient _gitHubClient = new();
    private readonly InMemorySnapshotStore _store = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task GetNotFollowingBackAsync_ReturnsFollowedAccountsMissingFromTheFollowersList()
    {
        _gitHubClient.Followers = FollowRelationshipReportTests.Users("alice", "bob", "carol");
        _gitHubClient.Following = FollowRelationshipReportTests.Users("bob", "dave");

        var result = await CreateService().GetNotFollowingBackAsync("someuser");

        Assert.Equal(["dave"], result.Select(user => user.Login));
    }

    [Fact]
    public async Task GetRelationshipReportAsync_ReusesTheCachedReport_ForRepeatLookups()
    {
        var service = CreateService();

        await service.GetRelationshipReportAsync("someuser");
        await service.GetRelationshipReportAsync("SomeUser");

        Assert.Equal(1, _gitHubClient.FollowersCalls);
        Assert.Equal(1, _gitHubClient.FollowingCalls);
    }

    [Fact]
    public async Task GetRelationshipReportAsync_RecordsOneSnapshotPerFreshFetch()
    {
        _gitHubClient.Followers = FollowRelationshipReportTests.Users("alice");
        _gitHubClient.Following = FollowRelationshipReportTests.Users("bob");
        var service = CreateService();

        await service.GetRelationshipReportAsync("SomeUser");
        await service.GetRelationshipReportAsync("someuser");

        var snapshot = Assert.Single(_store.Snapshots);
        Assert.Equal("someuser", snapshot.Username);
        Assert.Equal(1, snapshot.NotFollowingBackCount);
        Assert.Equal(_timeProvider.GetUtcNow(), snapshot.CapturedAt);
    }

    [Fact]
    public async Task GetRelationshipReportAsync_StillReturnsTheReport_WhenTheSnapshotQueueIsFull()
    {
        _store.QueueFull = true;
        _gitHubClient.Following = FollowRelationshipReportTests.Users("bob");

        var report = await CreateService().GetRelationshipReportAsync("someuser");

        Assert.Single(report.NotFollowingBack);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-bad")]
    [InlineData("has space")]
    public async Task GetRelationshipReportAsync_RejectsInvalidUsernames_WithoutCallingGitHub(string username)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().GetRelationshipReportAsync(username));

        Assert.Equal("username", exception.Field);
        Assert.Equal(0, _gitHubClient.FollowersCalls);
    }

    [Theory]
    [InlineData(2_001, 10)]
    [InlineData(10, 2_001)]
    public async Task GetRelationshipReportAsync_RefusesOversizedAccounts_BeforePagingAnyLists(int followers, int following)
    {
        _gitHubClient.Profile = new GitHubProfile(1, "someuser", followers, following);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().GetRelationshipReportAsync("someuser"));

        Assert.Contains("2000", exception.Message);
        Assert.Equal(0, _gitHubClient.FollowersCalls);
        Assert.Equal(0, _gitHubClient.FollowingCalls);
    }

    [Fact]
    public async Task GetRelationshipReportAsync_PropagatesNotFound()
    {
        _gitHubClient.ThrowOnFetch = new NotFoundException("GitHub user", "ghost");

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetRelationshipReportAsync("ghost"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetHistoryAsync_RejectsOutOfRangeLimits(int limit)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().GetHistoryAsync("someuser", limit));

        Assert.Equal("limit", exception.Field);
    }

    private GitHubUserService CreateService()
    {
        var cache = new ServiceCollection()
            .AddHybridCache().Services
            .BuildServiceProvider()
            .GetRequiredService<HybridCache>();

        return new GitHubUserService(
            _gitHubClient,
            _store,
            _store,
            cache,
            _timeProvider,
            Options.Create(new GitHubUserServiceOptions()),
            NullLogger<GitHubUserService>.Instance);
    }

    private sealed class FakeGitHubClient : IGitHubClient
    {
        public List<GitHubUser> Followers { get; set; } = [];

        public List<GitHubUser> Following { get; set; } = [];

        public Exception? ThrowOnFetch { get; set; }

        // Null means "derive the counts from the Followers/Following lists".
        public GitHubProfile? Profile { get; set; }

        public int FollowersCalls { get; private set; }

        public int FollowingCalls { get; private set; }

        public Task<GitHubProfile> GetProfileAsync(string username, CancellationToken cancellationToken = default) =>
            ThrowOnFetch is null
                ? Task.FromResult(Profile ?? new GitHubProfile(1, username, Followers.Count, Following.Count))
                : Task.FromException<GitHubProfile>(ThrowOnFetch);

        public Task<IReadOnlyList<GitHubUser>> GetFollowersAsync(string username, CancellationToken cancellationToken = default)
        {
            FollowersCalls++;
            return ThrowOnFetch is null ? Task.FromResult<IReadOnlyList<GitHubUser>>(Followers) : Task.FromException<IReadOnlyList<GitHubUser>>(ThrowOnFetch);
        }

        public Task<IReadOnlyList<GitHubUser>> GetFollowingAsync(string username, CancellationToken cancellationToken = default)
        {
            FollowingCalls++;
            return ThrowOnFetch is null ? Task.FromResult<IReadOnlyList<GitHubUser>>(Following) : Task.FromException<IReadOnlyList<GitHubUser>>(ThrowOnFetch);
        }
    }

    // Stands in for both the queue and the repository, so enqueued snapshots are immediately readable.
    private sealed class InMemorySnapshotStore : IFollowSnapshotQueue, IFollowSnapshotRepository
    {
        public List<FollowSnapshot> Snapshots { get; } = [];

        public bool QueueFull { get; set; }

        public bool TryEnqueue(FollowSnapshot snapshot)
        {
            if (QueueFull)
            {
                return false;
            }

            Snapshots.Add(snapshot);
            return true;
        }

        public Task AddRangeAsync(IReadOnlyCollection<FollowSnapshot> snapshots, CancellationToken cancellationToken = default)
        {
            Snapshots.AddRange(snapshots);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FollowSnapshot>> GetHistoryAsync(
            string username, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FollowSnapshot>>(Snapshots.Take(limit).ToList());
    }
}
