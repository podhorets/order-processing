using Contracts.Commands;
using Contracts.Events;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Features.ReleaseInventory;

public static class ReleaseInventoryHandler
{
    public static async Task<InventoryReleasedEvent> Handle(
        ReleaseInventoryCommand cmd,
        InventoryDbContext ctx,
        ILogger logger,
        CancellationToken ct)
    {
        var reservations = await ctx.Reservations
            .Where(r => r.OrderId == cmd.OrderId)
            .ToListAsync(ct);

        if (reservations.Count == 0)
        {
            // Already released — idempotent.
            logger.LogInformation("ReleaseInventory: no reservations found for order {OrderId}, skipping", cmd.OrderId);
            return new InventoryReleasedEvent(cmd.OrderId);
        }

        var skus = reservations.Select(r => r.Sku).ToList();
        var inventories = await ctx.Inventories
            .Where(i => skus.Contains(i.Sku))
            .ToDictionaryAsync(i => i.Sku, ct);

        foreach (var r in reservations)
            if (inventories.TryGetValue(r.Sku, out var inv))
                inv.Release(r.Quantity);

        ctx.Reservations.RemoveRange(reservations);
        await ctx.SaveChangesAsync(ct);

        logger.LogInformation("Inventory released for order {OrderId}", cmd.OrderId);
        return new InventoryReleasedEvent(cmd.OrderId);
    }
}
