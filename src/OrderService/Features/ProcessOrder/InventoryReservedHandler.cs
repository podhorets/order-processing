using OrderService.Domain.Enums;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using Shared.Contracts.Events.V1;

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

        // TODO: send PerformPayment outbox message with customerId, orderId, TotalAmount

        await ctx.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} processed", message.OrderId);
    }
}
