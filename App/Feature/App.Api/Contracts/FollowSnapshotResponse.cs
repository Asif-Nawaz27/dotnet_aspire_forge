using App.Domain.GitHub;

namespace App.Api.Contracts;

// The public shape of a snapshot, decoupled from the persisted entity so the schema can evolve
// without breaking API clients.
public sealed record FollowSnapshotResponse(
    DateTimeOffset CapturedAt,
    int FollowersCount,
    int FollowingCount,
    int NotFollowingBackCount,
    int NotFollowedBackCount)
{
    public static FollowSnapshotResponse From(FollowSnapshot snapshot) => new(
        snapshot.CapturedAt,
        snapshot.FollowersCount,
        snapshot.FollowingCount,
        snapshot.NotFollowingBackCount,
        snapshot.NotFollowedBackCount);
}
