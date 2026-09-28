using System.Threading.Channels;
using App.Application.Abstractions;
using App.Domain.GitHub;

namespace App.Infrastructure.Persistence;

// Bounded so a prolonged database outage costs a fixed amount of memory rather than growing without
// limit. In-process only: snapshots still queued when the process exits are lost, an accepted
// trade-off for data that is purely historical.
internal sealed class FollowSnapshotQueue : IFollowSnapshotQueue
{
    public const int Capacity = 1_000;

    private readonly Channel<FollowSnapshot> _channel = Channel.CreateBounded<FollowSnapshot>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public ChannelReader<FollowSnapshot> Reader => _channel.Reader;

    public bool TryEnqueue(FollowSnapshot snapshot) => _channel.Writer.TryWrite(snapshot);
}
