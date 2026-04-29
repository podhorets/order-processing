using Contracts.Commands;
using Contracts.Events;
using InventoryService.Domain.Entities;
using InventoryService.Infrastructure.Observability;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Features.ReserveInventory;

public static class ReserveInventoryHandler
{
    public static async Task<object> Handle(
        ReserveInventoryCommand cmd,
        InventoryDbContext ctx,
        InventoryMetrics metrics,
        ILogger logger,
        CancellationToken ct)
    {
        // Idempotency: reservations for this order already exist → ack as success.
        if (await ctx.Reservations.AnyAsync(r => r.OrderId == cmd.OrderId, ct))
        {
            logger.LogInformation("ReserveInventory: order {OrderId} already reserved, skipping", cmd.OrderId);
            return new InventoryReservedEvent(cmd.OrderId);
        }

        var skus = cmd.Items.Select(i => i.Sku).Distinct().OrderBy(s => s).ToArray();

        var inventories = await ctx.Inventories
            .Where(i => skus.Contains(i.Sku))
            .ToDictionaryAsync(i => i.Sku, ct);

        // Validate all items before reserving any (all-or-nothing).
        foreach (var item in cmd.Items)
        {
            if (!inventories.TryGetValue(item.Sku, out var inv))
            {
                metrics.ReservationFailed();
                return new InventoryReservationFailedEvent(cmd.OrderId, $"SKU not found: '{item.Sku}'");
            }

            if (inv.Available < item.Quantity)
            {
                metrics.ReservationFailed();
                return new InventoryReservationFailedEvent(
                    cmd.OrderId,
                    $"Insufficient stock for '{item.Sku}': requested {item.Quantity}, available {inv.Available}");
            }
        }

        // All valid — reserve and record reservations.
        foreach (var item in cmd.Items.OrderBy(i => i.Sku))
        {
            inventories[item.Sku].Reserve(item.Quantity);
            ctx.Reservations.Add(new Reservation(cmd.OrderId, item.Sku, item.Quantity));
        }

        // SaveChanges adds WHERE xmin = @orig → DbUpdateConcurrencyException on conflict.
        // Wolverine retries the entire handler automatically.
        await ctx.SaveChangesAsync(ct);

        metrics.ReservationSucceeded();
        logger.LogInformation("Inventory reserved for order {OrderId}", cmd.OrderId);

        // Wolverine outbox: event is written to the outbox in the same transaction
        // and delivered to RabbitMQ after commit.
        return new InventoryReservedEvent(cmd.OrderId);
    }
}
