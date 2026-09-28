using App.Domain.GitHub;

namespace App.Application.Abstractions;

// Hands snapshots off to be persisted in the background, so a slow or unavailable database never
// delays the request that produced them.
public interface IFollowSnapshotQueue
{
    // False if the queue is full and the snapshot was dropped.
    bool TryEnqueue(FollowSnapshot snapshot);
}
