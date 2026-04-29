using Contracts.Commands;
using Contracts.Events;
using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Observability;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Features.ProcessPayment;

public static class ProcessPaymentHandler
{
    public static async Task<PaymentSuccessfulEvent> Handle(
        ProcessPaymentCommand cmd,
        PaymentDbContext ctx,
        PaymentMetrics metrics,
        ILogger logger,
        CancellationToken ct)
    {
        // Idempotency: if we already processed this order, return success without re-inserting.
        if (await ctx.PaymentRecords.AnyAsync(p => p.OrderId == cmd.OrderId, ct))
        {
            logger.LogInformation("ProcessPayment: order {OrderId} already processed, skipping", cmd.OrderId);
            return new PaymentSuccessfulEvent(cmd.OrderId);
        }

        // Mock: always approves.
        // Real implementation: call payment provider; on failure return PaymentFailedEvent.
        ctx.PaymentRecords.Add(new PaymentRecord(cmd.OrderId, cmd.CustomerId, cmd.Amount, "Approved"));
        await ctx.SaveChangesAsync(ct);

        metrics.PaymentProcessed();
        logger.LogInformation("Payment approved for order {OrderId}, amount {Amount:C}", cmd.OrderId, cmd.Amount);

        return new PaymentSuccessfulEvent(cmd.OrderId);
    }
}
