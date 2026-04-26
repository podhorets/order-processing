using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderService.Contracts.Events.V1;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Messaging.Outbox;
using OrderService.Infrastructure.Persistence;
using UUIDNext;

namespace OrderService.Features.ReserveInventory;

public sealed class ReserveInventoryHandler(
    OrderDbContext ctx,
    ILogger<ReserveInventoryHandler> logger)
    : IMessageHandler<OrderSubmitted>
{
    public async Task HandleAsync(OrderSubmitted message, CancellationToken ct)
    {
        var order = await ctx.Orders.FindAsync([message.OrderId], ct);
        if (order is null)
            throw new InvalidOperationException($"Order {message.OrderId} not found");
        
        order.MarkProcessing();
        
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
            ctx.OutboxMessages.Add(new OutboxMessage
            {
                Id = Uuid.NewSequential(),
                MessageType = MessagingQueues.InventoryReservationFailed,
                Payload = JsonSerializer.Serialize(new InventoryReservationFailed(message.OrderId, failure)),
                Status = OutboxStatus.Pending,
                OccurredAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(ct);
            logger.LogWarning("Reservation failed for order {OrderId}: {Reason}", message.OrderId, failure);
            return;
        }

        foreach (var item in items)
        {
            inventories[item.Sku].Reserve(item.Quantity);
            ctx.Reservations.Add(new Reservation(message.OrderId, item.Sku, item.Quantity));
        }

        ctx.OutboxMessages.Add(new OutboxMessage
        {
            Id = Uuid.NewSequential(),
            MessageType = MessagingQueues.InventoryReserved,
            Payload = JsonSerializer.Serialize(new InventoryReserved(message.OrderId)),
            Status = OutboxStatus.Pending,
            OccurredAt = DateTime.UtcNow
        });

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Inventory reserved for order {OrderId}", message.OrderId);
    }
}
