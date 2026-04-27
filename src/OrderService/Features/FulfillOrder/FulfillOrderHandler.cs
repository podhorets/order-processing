using Microsoft.EntityFrameworkCore;
using OrderService.Contracts.Events.V1;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Features.FulfillOrder;

public sealed class FulfillOrderHandler(
    OrderDbContext ctx,
    OrderMetrics metrics,
    ILogger<FulfillOrderHandler> logger)
    : IMessageHandler<PaymentSuccessful>
{
    public async Task HandleAsync(PaymentSuccessful message, CancellationToken ct)
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

        var reservations = await ctx.Reservations
            .Where(r => r.OrderId == message.OrderId)
            .ToListAsync(ct);

        var skus = reservations
            .Select(r => r.Sku)
            .OrderBy(s => s)
            .ToArray();

        await using var tx = await ctx.Database.BeginTransactionAsync(ct);

        var inventories = await ctx.Inventories
            .FromSqlInterpolated($@"
                SELECT * FROM ""Inventories""
                WHERE ""Sku"" = ANY({skus})
                ORDER BY ""Sku""
                FOR UPDATE")
            .ToDictionaryAsync(i => i.Sku, ct);

        foreach (var reservation in reservations)
            if (inventories.TryGetValue(reservation.Sku, out var inv))
                inv.Fulfill(reservation.Quantity);

        order.MarkProcessed();
        
        ctx.Reservations.RemoveRange(reservations);

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        metrics.OrderProcessed();
        logger.LogInformation("Order {OrderId} fulfilled", message.OrderId);
    }
}
