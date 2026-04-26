using System.Text.Json;
using FluentValidation;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Domain.ValueObjects;
using OrderService.Infrastructure.Extensions;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using Shared.Contracts.Dto.V1;
using Shared.Contracts.Events.V1;
using UUIDNext;

namespace OrderService.Features.SubmitOrder;

public sealed class SubmitOrderEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapPost("/api/v1/orders", Handle)
            .WithName("SubmitOrder")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

    private static async Task<IResult> Handle(
        SubmitOrderRequest request,
        IValidator<SubmitOrderRequest> validator,
        OrderDbContext ctx,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var orderItems = request.OrderItems.Select(i => new OrderItemDraft(i.Sku, i.Quantity, i.UnitPrice)).ToList();
        var order = Order.Submit(request.CustomerId, orderItems);

        var outboxItems = order.Items.Select(i => new OrderItemDto(i.Sku, i.Quantity, i.UnitPrice)).ToList();
        var outboxMessage = new OutboxMessage
        {
            Id = Uuid.NewSequential(),
            MessageType = MessagingQueues.OrderSubmitted,
            Payload = JsonSerializer.Serialize(new OrderSubmitted(order.Id, order.CustomerId, outboxItems)),
            Status = OutboxStatus.Pending,
            OccurredAt = DateTime.UtcNow
        };

        ctx.Orders.Add(order);
        ctx.OutboxMessages.Add(outboxMessage);
        await ctx.SaveChangesAsync(ct);

        return Results.Accepted(
            $"/api/v1/orders/{order.Id}",
            new SubmitOrderResponse(order.Id, order.Status.ToString()));
    }
}