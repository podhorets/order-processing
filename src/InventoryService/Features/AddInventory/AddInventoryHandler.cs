using Contracts.Commands;
using InventoryService.Domain.Entities;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Features.AddInventory;

/// <summary>
/// Handles both the message-based path (from OrderService batch endpoints)
/// and is called directly by the HTTP AddInventoryEndpoint.
/// </summary>
public static class AddInventoryHandler
{
    public static async Task Handle(
        AddInventoryCommand cmd,
        InventoryDbContext ctx,
        ILogger logger,
        CancellationToken ct)
    {
        var existing = await ctx.Inventories
            .FirstOrDefaultAsync(i => i.Sku == cmd.Sku, ct);

        if (existing is not null)
        {
            existing.AddStock(cmd.OnHand);
            logger.LogInformation("Added {Qty} stock to existing SKU {Sku}", cmd.OnHand, cmd.Sku);
        }
        else
        {
            ctx.Inventories.Add(new Inventory(cmd.Sku, cmd.OnHand));
            logger.LogInformation("Created new inventory for SKU {Sku} with {Qty} on-hand", cmd.Sku, cmd.OnHand);
        }

        await ctx.SaveChangesAsync(ct);
    }
}
