using System.Text.Json;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using Shared.Contracts;
using Shared.Contracts.Commands.V1;
using Shared.Contracts.Events.V1;
using UUIDNext;

namespace OrderService.Features.ProcessOrder;

public sealed class InventoryReservedHandler(
    OrderDbContext ctx,
    ILogger<InventoryReservedHandler> logger)
    : IMessageHandler<InventoryReserved>
{
    public async Task HandleAsync(InventoryReserved message, CancellationToken ct)
    {
        var order = await ctx.Orders.FindAsync([message.OrderId], ct);

        if (order is null)
        {
            logger.LogWarning("Order {OrderId} not found", message.OrderId);
            return;
        }

        if (order.Status == OrderStatus.Processed)
        {
            logger.LogInformation("Order {OrderId} already processed, skipping", message.OrderId);
            return;
        }

        ctx.OutboxMessages.Add(new OutboxMessage
        {
            Id = Uuid.NewSequential(),
            MessageType = MessagingQueues.PerformPayment,
            Payload = JsonSerializer.Serialize(new PerformPayment(order.Id, order.CustomerId, order.TotalAmount)),
            Status = OutboxStatus.Pending,
            OccurredAt = DateTime.UtcNow
        });

        await ctx.SaveChangesAsync(ct);

        logger.LogInformation("PerformPayment outbox queued for order {OrderId}", message.OrderId);
    }
}
