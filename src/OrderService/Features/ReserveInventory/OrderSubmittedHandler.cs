using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using Shared.Contracts.Events.V1;

namespace OrderService.Features.ReserveInventory;

public sealed class OrderSubmittedHandler(
    OrderDbContext ctx,
    ILogger<OrderSubmittedHandler> logger)
    : IMessageHandler<OrderSubmitted>
{
    public async Task HandleAsync(OrderSubmitted message, CancellationToken ct)
    {
        var alreadyProcessed = await ctx.Reservations
            .AnyAsync(r => r.OrderId == message.OrderId, ct);

        if (alreadyProcessed)
        {
            logger.LogInformation("Order {OrderId} already reserved, skipping", message.OrderId);
            return;
        }

        var items = message.OrderItems
            .GroupBy(i => i.Sku)
            .Select(g => new { Sku = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .OrderBy(i => i.Sku)
            .ToArray();

        var skus = items.Select(i => i.Sku).ToArray();

        await using var tx = await ctx.Database.BeginTransactionAsync(ct);

        var inventories = await ctx.Inventories
            .FromSqlInterpolated($@"
                SELECT * FROM ""Inventories""
                WHERE ""Sku"" = ANY({skus})
                ORDER BY ""Sku""
                FOR UPDATE")
            .ToDictionaryAsync(x => x.Sku, ct);

        foreach (var item in items)
        {
            string? failure = null;

            if (!inventories.TryGetValue(item.Sku, out var inv))
                failure = $"{ReservationError.SkuNotFound}: '{item.Sku}'";
            else if (inv.Available < item.Quantity)
                failure = $"{ReservationError.InsufficientStock}: '{item.Sku}' requested {item.Quantity}, available {inv.Available}";

            if (failure is null) continue;

            await tx.RollbackAsync(ct);
            // TODO: outbox failure
            logger.LogWarning("Reservation failed for order {OrderId}: {Reason}",
                message.OrderId, failure);
            return;
        }

        foreach (var item in items)
        {
            inventories[item.Sku].Reserve(item.Quantity);
            ctx.Reservations.Add(new Reservation(message.OrderId, item.Sku, item.Quantity));
        }

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // TODO: outbox success
        logger.LogInformation("Inventory reserved for order {OrderId}", message.OrderId);
    }
}
