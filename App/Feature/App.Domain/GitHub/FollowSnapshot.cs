namespace App.Domain.GitHub;

// A point-in-time record of an account's follow counts, persisted each time a report is computed so
// changes can be tracked over time.
public sealed class FollowSnapshot
{
    public const int MaxUsernameLength = 39;

    private FollowSnapshot()
    {
        Username = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; }

    public int FollowersCount { get; private set; }

    public int FollowingCount { get; private set; }

    public int NotFollowingBackCount { get; private set; }

    public int NotFollowedBackCount { get; private set; }

    public DateTimeOffset CapturedAt { get; private set; }

    public static FollowSnapshot From(FollowRelationshipReport report, DateTimeOffset capturedAt) => new()
    {
        Id = Guid.CreateVersion7(capturedAt),
        Username = report.Username.ToLowerInvariant(),
        FollowersCount = report.FollowersCount,
        FollowingCount = report.FollowingCount,
        NotFollowingBackCount = report.NotFollowingBack.Count,
        NotFollowedBackCount = report.NotFollowedBack.Count,
        CapturedAt = capturedAt,
    };
}
