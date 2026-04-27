using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderService.Contracts;
using OrderService.Infrastructure.Persistence;
using OrderService.Contracts.Commands.V1;
using OrderService.Contracts.Events.V1;
using OrderService.Features.FulfillOrder;
using OrderService.Features.InitiatePayment;
using OrderService.Features.ProcessPayment;
using OrderService.Features.RejectOrder;
using OrderService.Features.ReserveInventory;

namespace OrderService.Infrastructure.Messaging;

public sealed class MessageDispatcher(IServiceScopeFactory scopeFactory) : IMessageDispatcher
{
    public async Task DispatchAsync(string messageType, string json, string messageId, CancellationToken ct)
    {
        switch (messageType)
        {
            case MessagingQueues.OrderSubmitted:
                await HandleAsync<OrderSubmitted, ReserveInventoryHandler>(json, messageId, ct);
                break;

            case MessagingQueues.InventoryReserved:
                await HandleAsync<InventoryReserved, InitiatePaymentHandler>(json, messageId, ct);
                break;

            case MessagingQueues.InventoryReservationFailed:
                await HandleAsync<InventoryReservationFailed, RejectOrderHandler>(json, messageId, ct);
                break;

            case MessagingQueues.PerformPayment:
                await HandleAsync<PerformPayment, ProcessPaymentHandler>(json, messageId, ct);
                break;

            case MessagingQueues.PaymentSuccessful:
                await HandleAsync<PaymentSuccessful, FulfillOrderHandler>(json, messageId, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown message type: '{messageType}'.");
        }
    }

    private async Task HandleAsync<T, THandler>(string json, string messageId, CancellationToken ct)
        where THandler : IMessageHandler<T>
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var claimed = await ctx.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "InboxMessages" ("MessageId", "ConsumedAt")
             VALUES ({messageId}, {DateTime.UtcNow})
             ON CONFLICT ("MessageId") DO NOTHING
             """, ct) > 0;

        if (!claimed) return;

        try
        {
            var message = JsonSerializer.Deserialize<T>(json)!;
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
            await handler.HandleAsync(message, ct);
        }
        catch
        {
            await ctx.Database.ExecuteSqlAsync(
                $"""DELETE FROM "InboxMessages" WHERE "MessageId" = {messageId}""", ct);
            throw;
        }
    }
}
