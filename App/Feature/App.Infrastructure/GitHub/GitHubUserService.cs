using System.Net.Http.Json;
using System.Text.Json.Serialization;
using App.Application;
using App.Domain;

namespace App.Infrastructure.GitHub;

public sealed class GitHubUserService(HttpClient httpClient) : IGitHubUserService
{
    private const int PageSize = 100;

    public async Task<IReadOnlyList<GitHubUser>> GetFollowersNotFollowingBackAsync(
        string username, CancellationToken cancellationToken = default)
    {
        var followers = await GetAllPagesAsync(username, "followers", cancellationToken);
        var following = await GetAllPagesAsync(username, "following", cancellationToken);

        var followerLogins = followers.Select(user => user.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return following
            .Where(follower => !followerLogins.Contains(follower.Login))
            .Select(follower => new GitHubUser(follower.Id, follower.Login, follower.AvatarUrl, follower.HtmlUrl))
            .ToList();
    }

    // GitHub paginates at 100/page max; loop until a short page signals there's nothing left.
    private async Task<List<GitHubUserDto>> GetAllPagesAsync(
        string username, string relationship, CancellationToken cancellationToken)
    {
        var results = new List<GitHubUserDto>();
        var page = 1;

        while (true)
        {
            var response = await httpClient.GetFromJsonAsync<List<GitHubUserDto>>(
                $"users/{username}/{relationship}?per_page={PageSize}&page={page}", cancellationToken);

            if (response is null || response.Count == 0)
            {
                break;
            }

            results.AddRange(response);

            if (response.Count < PageSize)
            {
                break;
            }

            page++;
        }

        return results;
    }

    private sealed record GitHubUserDto(
        long Id,
        string Login,
        [property: JsonPropertyName("avatar_url")] string AvatarUrl,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}
