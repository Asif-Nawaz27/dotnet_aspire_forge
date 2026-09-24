using App.Application;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

[ApiController]
[Route("api/github")]
public class GitHubController(IGitHubUserService gitHubUserService) : ControllerBase
{
    [HttpGet("{username}/followers-not-following-back")]
    public async Task<IActionResult> GetFollowersNotFollowingBack(
        string username, CancellationToken cancellationToken)
    {
        try
        {
            var result = await gitHubUserService.GetFollowersNotFollowingBackAsync(username, cancellationToken);
            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            // A real, easily-reproduced failure mode: GitHub's unauthenticated API allows only 60
            // requests/hour per IP, and a single popular account's followers alone can take dozens of
            // paginated requests. Surface this as a clear upstream failure instead of an opaque 500.
            return Problem(
                detail: $"Could not reach the GitHub API: {ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
