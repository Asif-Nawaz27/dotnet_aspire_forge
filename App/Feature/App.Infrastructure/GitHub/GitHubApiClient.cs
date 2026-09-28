using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using App.Application.Abstractions;
using App.Domain.Exceptions;
using App.Domain.GitHub;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.GitHub;

public sealed class GitHubApiClient(HttpClient httpClient, IOptions<GitHubOptions> options) : IGitHubClient
{
    private const string ServiceName = "GitHub";
    private const int PageSize = 100;

    public async Task<GitHubProfile> GetProfileAsync(string username, CancellationToken cancellationToken = default)
    {
        var profile = await GetJsonAsync<GitHubProfileDto>(
            $"users/{Uri.EscapeDataString(username)}", username, cancellationToken);

        return profile is null
            ? throw new UpstreamServiceException(ServiceName, "The API returned an empty profile.")
            : new GitHubProfile(profile.Id, profile.Login, profile.Followers, profile.Following);
    }

    public Task<IReadOnlyList<GitHubUser>> GetFollowersAsync(
        string username, CancellationToken cancellationToken = default) =>
        GetAllPagesAsync(username, "followers", cancellationToken);

    public Task<IReadOnlyList<GitHubUser>> GetFollowingAsync(
        string username, CancellationToken cancellationToken = default) =>
        GetAllPagesAsync(username, "following", cancellationToken);

    // GitHub paginates at 100/page max; loop until a short page signals there's nothing left.
    private async Task<IReadOnlyList<GitHubUser>> GetAllPagesAsync(
        string username, string relationship, CancellationToken cancellationToken)
    {
        var maxPages = options.Value.MaxPages;
        var results = new List<GitHubUser>();

        for (var page = 1; page <= maxPages; page++)
        {
            var users = await GetJsonAsync<List<GitHubUserDto>>(
                $"users/{Uri.EscapeDataString(username)}/{relationship}?per_page={PageSize}&page={page}",
                username,
                cancellationToken) ?? [];

            results.AddRange(users.Select(user => new GitHubUser(user.Id, user.Login, user.AvatarUrl, user.HtmlUrl)));

            if (users.Count < PageSize)
            {
                return results;
            }
        }

        // Backstop for the service's profile-count check (the list can grow between the two calls).
        // Returning a truncated list would silently produce a wrong comparison, so refuse instead.
        throw new ValidationException(
            nameof(username),
            $"'{username}' has more than {maxPages * PageSize} {relationship} entries, which exceeds the supported limit.");
    }

    private async Task<T?> GetJsonAsync<T>(string path, string username, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await httpClient.GetAsync(path, cancellationToken);
        }
        // Network failures, timeouts, and resilience-pipeline rejections (e.g. an open circuit breaker)
        // all mean the same thing to callers - but a cancellation the caller asked for is not a failure.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpstreamServiceException(ServiceName, "The API could not be reached.", innerException: ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException("GitHub user", username);
            }

            if (IsRateLimited(response))
            {
                throw new UpstreamServiceException(
                    ServiceName, "API rate limit exceeded.", GetRetryAfter(response));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamServiceException(
                    ServiceName, $"The API responded with {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
    }

    // GitHub signals primary rate limits with 403 + x-ratelimit-remaining: 0, and secondary ones with 429.
    private static bool IsRateLimited(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.TooManyRequests
        || (response.StatusCode == HttpStatusCode.Forbidden
            && response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining)
            && remaining.FirstOrDefault() == "0");

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.TryGetValues("x-ratelimit-reset", out var values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var resetEpoch))
        {
            var wait = DateTimeOffset.FromUnixTimeSeconds(resetEpoch) - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    private sealed record GitHubProfileDto(long Id, string Login, int Followers, int Following);

    private sealed record GitHubUserDto(
        long Id,
        string Login,
        [property: JsonPropertyName("avatar_url")] string AvatarUrl,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}
