using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Messaging.Outbox;

public sealed class OutboxProcessor(
    OrderDbContext ctx,
    RabbitMqPublisher publisher,
    IOptions<OutboxSettings> settings,
    ILogger<OutboxProcessor> logger)
{
    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        await publisher.EnsureInitializedAsync(ct);

        await using var tx = await ctx.Database.BeginTransactionAsync(ct);

        var messages = await ctx.OutboxMessages
            .FromSqlRaw("""
                SELECT *
                FROM "OutboxMessages"
                WHERE "Status" = 'Pending'
                ORDER BY "OccurredAt"
                FOR UPDATE SKIP LOCKED
                LIMIT {0}
                """, settings.Value.BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var msg in messages)
        {
            try
            {
                await publisher.PublishAsync(msg.MessageType, msg.Payload, ct);
                msg.Status = OutboxStatus.Processed;
                msg.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                msg.RetryCount++;

                logger.LogWarning(ex,
                    "Failed to publish outbox message {Id} (type: {Type}), attempt {Retry}/{Max}",
                    msg.Id, msg.MessageType, msg.RetryCount, settings.Value.MaxRetries);

                if (msg.RetryCount >= settings.Value.MaxRetries)
                {
                    msg.Status = OutboxStatus.Failed;
                    msg.ProcessedAt = DateTime.UtcNow;
                    msg.Error = ex.ToString();

                    logger.LogCritical(
                        "Outbox message {Id} (type: {Type}) dead-lettered after {Retries} attempts. " +
                        "Manual intervention required.",
                        msg.Id, msg.MessageType, msg.RetryCount);
                }
            }
        }

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
