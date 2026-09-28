using System.Net;
using System.Net.Http.Headers;

namespace App.Api.IntegrationTests;

// Cross-cutting behaviour of the HTTP pipeline, independent of any one endpoint.
public class PipelineTests
{
    [Theory]
    [InlineData("/alive")]
    [InlineData("/health")]
    public async Task HealthEndpoints_ReportHealthy(string path)
    {
        using var factory = new AppApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SecureEndpoint_Returns401_WithoutAToken()
    {
        using var factory = new AppApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GitHubEndpoints_Return429_OncePerClientBudgetIsSpent()
    {
        using var factory = new AppApiFactory { GitHubPermitLimit = 2 };
        factory.GitHub.Accounts["someuser"] = ([], []);
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.GetAsync("/api/github/someuser/relationship")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Responses_AreCompressed_WhenTheClientAcceptsIt()
    {
        using var factory = new AppApiFactory();
        factory.GitHub.Accounts["someuser"] = ([], Enumerable.Range(0, 200).Select(i => $"user{i}").ToArray());
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/github/someuser/relationship");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("br", response.Content.Headers.ContentEncoding);
    }
}
