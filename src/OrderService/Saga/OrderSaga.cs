using Contracts.Commands;
using Contracts.Events;
using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;
using Wolverine;

namespace OrderService.Saga;

public class OrderSaga : Wolverine.Saga
{
    // Wolverine correlates incoming events to this saga by matching event.OrderId == saga.Id
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderSagaStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // ── Start: triggered by local SubmitOrderCommand ─────────────────────────

    public static async Task<(OrderSaga, ReserveInventoryCommand, OrderSagaTimeout)> StartAsync(
        SubmitOrderCommand cmd,
        OrderDbContext ctx,
        CancellationToken ct)
    {
        var saga = new OrderSaga
        {
            Id         = cmd.OrderId,
            CustomerId = cmd.CustomerId,
            TotalAmount = cmd.TotalAmount,
            Status     = OrderSagaStatus.ReservingInventory,
            CreatedAt  = DateTime.UtcNow
        };

        // Write the read-model row so GetOrder always has something to return.
        ctx.OrderSummaries.Add(new OrderSummary
        {
            Id             = cmd.OrderId,
            CustomerId     = cmd.CustomerId,
            TotalAmount    = cmd.TotalAmount,
            Status         = OrderSagaStatus.ReservingInventory.ToString(),
            CreatedAt      = saga.CreatedAt
        });

        return (saga,
                new ReserveInventoryCommand(cmd.OrderId, cmd.Items),
                new OrderSagaTimeout(cmd.OrderId));
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    public async Task<ProcessPaymentCommand?> HandleAsync(
        InventoryReservedEvent evt,
        OrderDbContext ctx,
        CancellationToken ct)
    {
        if (Status != OrderSagaStatus.ReservingInventory) return null;
        Status = OrderSagaStatus.ProcessingPayment;
        await UpdateSummaryAsync(ctx, Status, ct);
        return new ProcessPaymentCommand(Id, CustomerId, TotalAmount);
    }

    public async Task<FulfillInventoryCommand?> HandleAsync(
        PaymentSuccessfulEvent evt,
        OrderDbContext ctx,
        CancellationToken ct)
    {
        if (Status != OrderSagaStatus.ProcessingPayment) return null;
        Status = OrderSagaStatus.Fulfilling;
        await UpdateSummaryAsync(ctx, Status, ct);
        return new FulfillInventoryCommand(Id);
    }

    public async Task HandleAsync(
        InventoryFulfilledEvent evt,
        OrderDbContext ctx,
        OrderMetrics metrics,
        CancellationToken ct)
    {
        if (Status != OrderSagaStatus.Fulfilling) return;
        Status      = OrderSagaStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        await UpdateSummaryAsync(ctx, Status, ct, CompletedAt);
        metrics.OrderCompleted();
        MarkCompleted();
    }

    // ── Reservation failed → reject immediately ───────────────────────────────

    public async Task HandleAsync(
        InventoryReservationFailedEvent evt,
        OrderDbContext ctx,
        OrderMetrics metrics,
        CancellationToken ct)
    {
        if (Status != OrderSagaStatus.ReservingInventory) return;
        Status          = OrderSagaStatus.Failed;
        RejectionReason = evt.Reason;
        CompletedAt     = DateTime.UtcNow;
        await UpdateSummaryAsync(ctx, Status, ct, CompletedAt, evt.Reason);
        metrics.OrderRejected();
        MarkCompleted();
    }

    // ── Payment failed → release reservation first ────────────────────────────

    public async Task<ReleaseInventoryCommand?> HandleAsync(
        PaymentFailedEvent evt,
        OrderDbContext ctx,
        CancellationToken ct)
    {
        if (Status != OrderSagaStatus.ProcessingPayment) return null;
        Status          = OrderSagaStatus.Compensating;
        RejectionReason = evt.Reason;
        await UpdateSummaryAsync(ctx, Status, ct);
        return new ReleaseInventoryCommand(Id);
    }

    public async Task HandleAsync(
        InventoryReleasedEvent evt,
        OrderDbContext ctx,
        OrderMetrics metrics,
        CancellationToken ct)
    {
        if (Status != OrderSagaStatus.Compensating) return;
        Status      = OrderSagaStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        await UpdateSummaryAsync(ctx, Status, ct, CompletedAt, RejectionReason);
        metrics.OrderRejected();
        MarkCompleted();
    }

    // ── Timeout ───────────────────────────────────────────────────────────────

    public async Task HandleAsync(
        OrderSagaTimeout timeout,
        OrderDbContext ctx,
        OrderMetrics metrics,
        CancellationToken ct)
    {
        if (Status is OrderSagaStatus.Completed or OrderSagaStatus.Failed) return;
        Status      = OrderSagaStatus.TimedOut;
        CompletedAt = DateTime.UtcNow;
        await UpdateSummaryAsync(ctx, Status, ct, CompletedAt);
        metrics.OrderTimedOut();
        MarkCompleted();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task UpdateSummaryAsync(
        OrderDbContext ctx,
        OrderSagaStatus status,
        CancellationToken ct,
        DateTime? completedAt = null,
        string? rejectionReason = null)
    {
        await ctx.OrderSummaries
            .Where(s => s.Id == Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, status.ToString())
                .SetProperty(p => p.CompletedAt, completedAt)
                .SetProperty(p => p.RejectionReason, rejectionReason), ct);
    }
}
