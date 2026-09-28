namespace App.Api.Configuration;

public static class RateLimitPolicies
{
    // Endpoints that spend the shared GitHub API quota get a much tighter per-client budget.
    public const string GitHub = "github";
}

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    // Per client IP, per minute, across every endpoint.
    public int GlobalPermitLimit { get; set; } = 100;

    // Per client IP, per sliding minute, for endpoints under RateLimitPolicies.GitHub.
    public int GitHubPermitLimit { get; set; } = 10;
}
