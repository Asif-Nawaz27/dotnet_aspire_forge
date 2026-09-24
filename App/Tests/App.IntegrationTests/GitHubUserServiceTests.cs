using System.Net;
using System.Text;
using App.Infrastructure.GitHub;

namespace App.IntegrationTests;

// Uses a fake HttpMessageHandler rather than calling the real GitHub API - that's what
// GitHubController hit against a live run during manual verification: GitHub's unauthenticated API
// allows only 60 requests/hour per IP, and a popular account's followers alone can take dozens of
// paginated requests, so a test that depends on the real API would be flaky by design.
public class GitHubUserServiceTests
{
    [Fact]
    public async Task GetFollowersNotFollowingBackAsync_ReturnsOnlyFollowersMissingFromTheFollowingList()
    {
        var handler = new FakeGitHubHandler(
            followers: ["alice", "bob", "carol"],
            following: ["bob"]);

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var service = new GitHubUserService(httpClient);

        var result = await service.GetFollowersNotFollowingBackAsync("someuser");

        Assert.Equal(["alice", "carol"], result.Select(user => user.Login).OrderBy(login => login));
    }

    [Fact]
    public async Task GetFollowersNotFollowingBackAsync_ReturnsEmpty_WhenEveryFollowerIsFollowedBack()
    {
        var handler = new FakeGitHubHandler(
            followers: ["alice", "bob"],
            following: ["alice", "bob", "carol"]);

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var service = new GitHubUserService(httpClient);

        var result = await service.GetFollowersNotFollowingBackAsync("someuser");

        Assert.Empty(result);
    }

    private sealed class FakeGitHubHandler(IReadOnlyList<string> followers, IReadOnlyList<string> following)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var logins = request.RequestUri!.AbsolutePath.EndsWith("/followers") ? followers : following;

            // Only the first page is exercised here - GetAllPagesAsync's own pagination loop is a
            // plain "keep going until a short page" mechanism, not this test's concern.
            var json = "[" + string.Join(",", logins.Select((login, index) =>
                $$"""{"id":{{index + 1}},"login":"{{login}}","avatar_url":"a","html_url":"h"}""")) + "]";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
