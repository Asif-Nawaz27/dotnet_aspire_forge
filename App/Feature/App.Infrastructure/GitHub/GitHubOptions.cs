using System.ComponentModel.DataAnnotations;

namespace App.Infrastructure.GitHub;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://api.github.com/");

    [Required]
    public string UserAgent { get; set; } = "AspireForge-App";

    // Optional personal access token. Without one GitHub allows 60 requests/hour per IP; with one,
    // 5,000/hour. Supply it via user-secrets or an environment variable (GitHub__Token), never appsettings.
    public string? Token { get; set; }

    // GitHub pages at 100 users max, so this caps a single list at MaxPages * 100 entries. A backstop
    // behind GitHubReports:MaxRelationshipSize, set with some slack above it.
    [Range(1, 100)]
    public int MaxPages { get; set; } = 25;
}
