using System.Text.Json;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using Shared.Contracts;
using Shared.Contracts.Commands.V1;
using Shared.Contracts.Events.V1;
using UUIDNext;

namespace OrderService.Features.ProcessOrder;

public sealed class PerformPaymentHandler(
    OrderDbContext ctx,
    ILogger<PerformPaymentHandler> logger)
    : IMessageHandler<PerformPayment>
{
    public async Task HandleAsync(PerformPayment message, CancellationToken ct)
    {
        // here we mock payment and handle only happy path
        // in case the payment was not successful, we would send PaymentFailed event
        // PaymentFailed event would remove inventory reservation and set Order status to rejected with approtiate error msg
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
