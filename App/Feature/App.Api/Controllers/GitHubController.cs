using System.ComponentModel.DataAnnotations;
using App.Api.Configuration;
using App.Api.Contracts;
using App.Application.GitHub;
using App.Domain.GitHub;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace App.Api.Controllers;

// Failures (invalid username, unknown user, GitHub rate limits or outages) surface as exceptions and
// are translated to problem details by GlobalExceptionHandler, so actions only handle the happy path.
[ApiController]
[Route("api/github/{username}")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public class GitHubController(IGitHubUserService gitHubUserService) : ControllerBase
{
    // Accounts the user follows that don't follow it back.
    [HttpGet("followers-not-following-back")]
    [EnableRateLimiting(RateLimitPolicies.GitHub)]
    [ProducesResponseType<IReadOnlyList<GitHubUser>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<GitHubUser>>> GetNotFollowingBack(
        [RegularExpression(GitHubUsername.Pattern)] string username, CancellationToken cancellationToken) =>
        Ok(await gitHubUserService.GetNotFollowingBackAsync(username, cancellationToken));

    // Both directions of the relationship plus totals, from a single (cached) GitHub fetch.
    [HttpGet("relationship")]
    [EnableRateLimiting(RateLimitPolicies.GitHub)]
    [ProducesResponseType<FollowRelationshipReport>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FollowRelationshipReport>> GetRelationship(
        [RegularExpression(GitHubUsername.Pattern)] string username, CancellationToken cancellationToken) =>
        Ok(await gitHubUserService.GetRelationshipReportAsync(username, cancellationToken));

    // Previously recorded snapshots, newest first. Reads only from the database - no GitHub quota spent.
    [HttpGet("history")]
    [ProducesResponseType<IReadOnlyList<FollowSnapshotResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FollowSnapshotResponse>>> GetHistory(
        [RegularExpression(GitHubUsername.Pattern)] string username,
        [FromQuery, Range(1, 100)] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var history = await gitHubUserService.GetHistoryAsync(username, limit, cancellationToken);
        return Ok(history.Select(FollowSnapshotResponse.From).ToList());
    }
}
