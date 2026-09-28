using System.ComponentModel.DataAnnotations;

namespace App.Application.GitHub;

public sealed class GitHubUserServiceOptions
{
    public const string SectionName = "GitHubReports";

    // How long a computed report is reused before GitHub is queried again. GitHub's unauthenticated
    // API allows only 60 requests/hour, so this is what keeps repeat lookups affordable.
    [Range(typeof(TimeSpan), "00:00:00", "1.00:00:00")]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    [Range(1, 500)]
    public int MaxHistoryItems { get; set; } = 100;

    // Accounts with more followers or following than this are refused up front: each 100 entries costs
    // one GitHub request, so a very large account would drain the whole hourly quota in one lookup.
    [Range(1, 10_000)]
    public int MaxRelationshipSize { get; set; } = 2_000;
}
