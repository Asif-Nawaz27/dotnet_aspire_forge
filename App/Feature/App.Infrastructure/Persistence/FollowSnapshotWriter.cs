using App.Application.Abstractions;
using App.Domain.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistence;

// Drains FollowSnapshotQueue into the database in batches. A failed batch is logged and dropped rather
// than retried forever - the EF execution strategy has already retried transient errors by then.
internal sealed partial class FollowSnapshotWriter(
    FollowSnapshotQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<FollowSnapshotWriter> logger) : BackgroundService
{
    private const int MaxBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<FollowSnapshot>(MaxBatchSize);

        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < MaxBatchSize && queue.Reader.TryRead(out var snapshot))
                {
                    batch.Add(snapshot);
                }

                await WriteBatchAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task WriteBatchAsync(List<FollowSnapshot> batch, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IFollowSnapshotRepository>();
            await repository.AddRangeAsync(batch, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogBatchFailed(ex, batch.Count);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to persist {Count} follow snapshots; they were dropped")]
    private partial void LogBatchFailed(Exception exception, int count);
}
