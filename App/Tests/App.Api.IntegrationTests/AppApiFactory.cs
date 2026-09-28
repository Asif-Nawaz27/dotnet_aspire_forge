using App.Application.Abstractions;
using App.Domain.GitHub;
using App.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.Api.IntegrationTests;

// Hosts the real Api pipeline (middleware, controllers, exception handling, rate limiting, DI) with
// only the two external dependencies swapped out: GitHub for a scriptable fake, Postgres for EF Core's
// in-memory provider. Each factory instance gets its own database.
public sealed class AppApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    public FakeGitHubClient GitHub { get; } = new();

    public int GitHubPermitLimit { get; init; } = 1_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", "Host=unused");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("RateLimiting:GlobalPermitLimit", "1000");
        builder.UseSetting("RateLimiting:GitHubPermitLimit", GitHubPermitLimit.ToString());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IGitHubClient>();
            services.AddSingleton<IGitHubClient>(GitHub);
        });
    }
}

public sealed class FakeGitHubClient : IGitHubClient
{
    public Dictionary<string, (string[] Followers, string[] Following)> Accounts { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Exception? ThrowOnFetch { get; set; }

    public async Task<GitHubProfile> GetProfileAsync(string username, CancellationToken cancellationToken = default)
    {
        var followers = await GetFollowersAsync(username, cancellationToken);
        var following = await GetFollowingAsync(username, cancellationToken);
        return new GitHubProfile(1, username, followers.Count, following.Count);
    }

    public Task<IReadOnlyList<GitHubUser>> GetFollowersAsync(string username, CancellationToken cancellationToken = default) =>
        Fetch(username, account => account.Followers);

    public Task<IReadOnlyList<GitHubUser>> GetFollowingAsync(string username, CancellationToken cancellationToken = default) =>
        Fetch(username, account => account.Following);

    private Task<IReadOnlyList<GitHubUser>> Fetch(
        string username, Func<(string[] Followers, string[] Following), string[]> select)
    {
        if (ThrowOnFetch is not null)
        {
            return Task.FromException<IReadOnlyList<GitHubUser>>(ThrowOnFetch);
        }

        if (!Accounts.TryGetValue(username, out var account))
        {
            return Task.FromException<IReadOnlyList<GitHubUser>>(
                new App.Domain.Exceptions.NotFoundException("GitHub user", username));
        }

        IReadOnlyList<GitHubUser> users = select(account)
            .Select((login, index) => new GitHubUser(index + 1, login, "avatar", "html"))
            .ToList();

        return Task.FromResult(users);
    }
}
