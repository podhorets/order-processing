using Contracts.Commands;
using Contracts.Events;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Features.FulfillInventory;

public static class FulfillInventoryHandler
{
    public static async Task<InventoryFulfilledEvent> Handle(
        FulfillInventoryCommand cmd,
        InventoryDbContext ctx,
        ILogger logger,
        CancellationToken ct)
    {
        var reservations = await ctx.Reservations
            .Where(r => r.OrderId == cmd.OrderId)
            .ToListAsync(ct);

        if (reservations.Count == 0)
        {
            // Already fulfilled — idempotent.
            logger.LogInformation("FulfillInventory: no reservations found for order {OrderId}, skipping", cmd.OrderId);
            return new InventoryFulfilledEvent(cmd.OrderId);
        }

        var skus = reservations.Select(r => r.Sku).ToList();
        var inventories = await ctx.Inventories
            .Where(i => skus.Contains(i.Sku))
            .ToDictionaryAsync(i => i.Sku, ct);

        foreach (var r in reservations)
            if (inventories.TryGetValue(r.Sku, out var inv))
                inv.Fulfill(r.Quantity);

        ctx.Reservations.RemoveRange(reservations);
        await ctx.SaveChangesAsync(ct);

        logger.LogInformation("Inventory fulfilled for order {OrderId}", cmd.OrderId);
        return new InventoryFulfilledEvent(cmd.OrderId);
    }
}
