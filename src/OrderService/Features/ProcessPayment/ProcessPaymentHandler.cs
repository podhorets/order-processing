using System.Text.Json;
using OrderService.Contracts;
using OrderService.Contracts.Commands.V1;
using OrderService.Contracts.Events.V1;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Messaging.Outbox;
using OrderService.Infrastructure.Persistence;
using UUIDNext;

namespace OrderService.Features.ProcessPayment;

public sealed class ProcessPaymentHandler(
    OrderDbContext ctx,
    ILogger<ProcessPaymentHandler> logger)
    : IMessageHandler<PerformPayment>
{
    public async Task HandleAsync(PerformPayment message, CancellationToken ct)
    {
        // Mock: happy path only.
        // On real failure: publish PaymentFailed, release reservation, reject order.
        ctx.OutboxMessages.Add(new OutboxMessage
        {
            Id = Uuid.NewSequential(),
            MessageType = MessagingQueues.PaymentSuccessful,
            Payload = JsonSerializer.Serialize(new PaymentSuccessful(message.OrderId)),
            Status = OutboxStatus.Pending,
            OccurredAt = DateTime.UtcNow
        });

        await ctx.SaveChangesAsync(ct);

        logger.LogInformation("Payment processed for order {OrderId}", message.OrderId);
    }
}
