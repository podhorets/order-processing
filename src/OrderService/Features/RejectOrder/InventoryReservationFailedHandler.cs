using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using Shared.Contracts.Events.V1;

namespace OrderService.Features.RejectOrder;

public sealed class InventoryReservationFailedHandler(
    OrderDbContext ctx,
    ILogger<InventoryReservationFailedHandler> logger)
    : IMessageHandler<InventoryReservationFailed>
{
    public async Task HandleAsync(InventoryReservationFailed message, CancellationToken ct)
    {
        var order = await ctx.Orders.FindAsync([message.OrderId], ct);

        if (order is null)
        {
            logger.LogWarning("Order {OrderId} not found for rejection", message.OrderId);
            return;
        }

        if (order.Status is OrderStatus.Rejected)
        {
            logger.LogInformation("Order {OrderId} already rejected, skipping", message.OrderId);
            return;
        }

        order.Reject(message.Reason);
        await ctx.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} rejected: {Reason}", message.OrderId, message.Reason);
    }
}
