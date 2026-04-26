using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OrderService.Infrastructure.Messaging.Outbox;

public sealed class OutboxSignalingInterceptor(OutboxChannel channel) : DbTransactionInterceptor
{
    public override void TransactionCommitted(
        DbTransaction transaction, TransactionEndEventData eventData)
        => channel.Signal();

    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        channel.Signal();
        return Task.CompletedTask;
    }
}
