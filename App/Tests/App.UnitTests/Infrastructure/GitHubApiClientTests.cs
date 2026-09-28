using System.Net;
using System.Text;
using App.Domain.Exceptions;
using App.Infrastructure.GitHub;
using Microsoft.Extensions.Options;

namespace App.UnitTests.Infrastructure;

// Uses a fake HttpMessageHandler rather than the real GitHub API: its unauthenticated limit is 60
// requests/hour per IP, so a test depending on it would be flaky by design.
public class GitHubApiClientTests
{
    [Fact]
    public async Task GetFollowersAsync_FollowsPagination_UntilAShortPage()
    {
        var handler = new FakeHandler(request =>
            request.RequestUri!.Query.EndsWith("&page=1")
                ? Json(UsersJson(100, offset: 0))
                : Json(UsersJson(3, offset: 100)));

        var users = await CreateClient(handler).GetFollowersAsync("someuser");

        Assert.Equal(103, users.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.StartsWith("/users/someuser/followers", request.RequestUri!.AbsolutePath));
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsCounts_FromASingleRequest()
    {
        var handler = new FakeHandler(_ => Json("""{"id":583231,"login":"octocat","followers":21000,"following":9}"""));

        var profile = await CreateClient(handler).GetProfileAsync("octocat");

        Assert.Equal(new App.Domain.GitHub.GitHubProfile(583231, "octocat", 21000, 9), profile);
        Assert.Equal("/users/octocat", Assert.Single(handler.Requests).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetProfileAsync_ThrowsNotFound_ForUnknownUsers()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<NotFoundException>(() => CreateClient(handler).GetProfileAsync("ghost"));
    }

    [Fact]
    public async Task GetFollowingAsync_RefusesToTruncate_WhenMaxPagesIsExceeded()
    {
        var handler = new FakeHandler(_ => Json(UsersJson(100, offset: 0)));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateClient(handler, maxPages: 2).GetFollowingAsync("someuser"));

        Assert.Contains("200", exception.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetFollowersAsync_ThrowsNotFound_ForUnknownUsers()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Assert.ThrowsAsync<NotFoundException>(() => CreateClient(handler).GetFollowersAsync("ghost"));
    }

    [Fact]
    public async Task GetFollowersAsync_ReportsRateLimitWithRetryAfter_WhenGitHubQuotaIsExhausted()
    {
        var reset = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();
        var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("x-ratelimit-remaining", "0");
            response.Headers.Add("x-ratelimit-reset", reset.ToString());
            return response;
        });

        var exception = await Assert.ThrowsAsync<UpstreamServiceException>(
            () => CreateClient(handler).GetFollowersAsync("someuser"));

        Assert.NotNull(exception.RetryAfter);
        Assert.InRange(exception.RetryAfter.Value, TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(31));
    }

    [Fact]
    public async Task GetFollowersAsync_WrapsServerErrors_AsUpstreamFailures()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var exception = await Assert.ThrowsAsync<UpstreamServiceException>(
            () => CreateClient(handler).GetFollowersAsync("someuser"));

        Assert.Null(exception.RetryAfter);
    }

    [Fact]
    public async Task GetFollowersAsync_WrapsNetworkFailures_AsUpstreamFailures()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("Connection refused"));

        var exception = await Assert.ThrowsAsync<UpstreamServiceException>(
            () => CreateClient(handler).GetFollowersAsync("someuser"));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetFollowersAsync_LetsCallerCancellationPropagate()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHandler(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient(handler).GetFollowersAsync("someuser", cts.Token));
    }

    private static GitHubApiClient CreateClient(FakeHandler handler, int maxPages = 20) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
            Options.Create(new GitHubOptions { MaxPages = maxPages }));

    private static string UsersJson(int count, int offset) =>
        "[" + string.Join(",", Enumerable.Range(offset, count).Select(index =>
            $$"""{"id":{{index}},"login":"user{{index}}","avatar_url":"a","html_url":"h"}""")) + "]";

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
