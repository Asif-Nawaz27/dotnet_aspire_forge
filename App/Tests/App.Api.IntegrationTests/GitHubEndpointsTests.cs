using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.Domain.Exceptions;

namespace App.Api.IntegrationTests;

public class GitHubEndpointsTests : IDisposable
{
    private readonly AppApiFactory _factory = new();
    private readonly HttpClient _client;

    public GitHubEndpointsTests()
    {
        _factory.GitHub.Accounts["someuser"] = (Followers: ["alice", "bob"], Following: ["bob", "carol"]);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task FollowersNotFollowingBack_ReturnsAccountsThatDoNotFollowBack()
    {
        var users = await _client.GetFromJsonAsync<JsonElement>("/api/github/someuser/followers-not-following-back");

        Assert.Equal(["carol"], users.EnumerateArray().Select(user => user.GetProperty("login").GetString()));
    }

    [Fact]
    public async Task Relationship_ReturnsBothDirectionsAndTotals()
    {
        var report = await _client.GetFromJsonAsync<JsonElement>("/api/github/someuser/relationship");

        Assert.Equal(2, report.GetProperty("followersCount").GetInt32());
        Assert.Equal(2, report.GetProperty("followingCount").GetInt32());
        Assert.Equal("carol", report.GetProperty("notFollowingBack")[0].GetProperty("login").GetString());
        Assert.Equal("alice", report.GetProperty("notFollowedBack")[0].GetProperty("login").GetString());
    }

    [Fact]
    public async Task History_ReturnsTheSnapshotRecordedByAPreviousLookup()
    {
        await _client.GetAsync("/api/github/someuser/relationship");

        // Snapshots are written by a background service, so poll briefly rather than expecting them
        // to be visible the instant the lookup returns.
        var history = default(JsonElement);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            history = await _client.GetFromJsonAsync<JsonElement>("/api/github/someuser/history?limit=5");
            if (history.GetArrayLength() > 0)
            {
                break;
            }

            await Task.Delay(100);
        }

        var snapshot = Assert.Single(history.EnumerateArray());
        Assert.Equal(1, snapshot.GetProperty("notFollowingBackCount").GetInt32());
        Assert.Equal(1, snapshot.GetProperty("notFollowedBackCount").GetInt32());
    }

    [Theory]
    [InlineData("/api/github/-invalid-/relationship")]
    [InlineData("/api/github/someuser/history?limit=0")]
    public async Task InvalidInput_Returns400ValidationProblem(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownUser_Returns404Problem()
    {
        var response = await _client.GetAsync("/api/github/ghost/relationship");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("ghost", problem.GetProperty("detail").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task GitHubRateLimited_Returns503WithRetryAfter()
    {
        _factory.GitHub.ThrowOnFetch = new UpstreamServiceException("GitHub", "API rate limit exceeded.", TimeSpan.FromSeconds(90));

        var response = await _client.GetAsync("/api/github/someuser/relationship");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(90), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task GitHubFailure_Returns502Problem()
    {
        _factory.GitHub.ThrowOnFetch = new UpstreamServiceException("GitHub", "The API could not be reached.");

        var response = await _client.GetAsync("/api/github/someuser/relationship");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedException_Returns500_WithoutLeakingDetails()
    {
        _factory.GitHub.ThrowOnFetch = new InvalidOperationException("secret internal detail");

        var response = await _client.GetAsync("/api/github/someuser/relationship");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("secret internal detail", await response.Content.ReadAsStringAsync());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
