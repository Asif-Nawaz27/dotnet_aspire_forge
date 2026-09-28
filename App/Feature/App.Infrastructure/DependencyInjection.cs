using System.Net.Http.Headers;
using App.Application.Abstractions;
using App.Infrastructure.GitHub;
using App.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace App.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Postgres";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddGitHub();

        return services;
    }

    // Applies pending EF Core migrations. Called at startup only when explicitly enabled - in a
    // multi-instance deployment, run migrations as a separate release step instead.
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            // Bounded retries: the defaults (6 retries, up to 30s apart) can hold a request for minutes.
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null));
        });

        services.AddScoped<IFollowSnapshotRepository, FollowSnapshotRepository>();

        services.AddSingleton<FollowSnapshotQueue>();
        services.AddSingleton<IFollowSnapshotQueue>(provider => provider.GetRequiredService<FollowSnapshotQueue>());
        services.AddHostedService<FollowSnapshotWriter>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"]);
    }

    private static void AddGitHub(this IServiceCollection services)
    {
        services.AddOptions<GitHubOptions>()
            .BindConfiguration(GitHubOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Resilience (retries, circuit breaker, timeouts) is applied to every HttpClient by
        // ServiceDefaults' ConfigureHttpClientDefaults.
        services.AddHttpClient<IGitHubClient, GitHubApiClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<GitHubOptions>>().Value;

            client.BaseAddress = options.BaseAddress;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

            if (!string.IsNullOrWhiteSpace(options.Token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
            }
        });
    }
}
