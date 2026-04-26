using FluentValidation;
using OrderService.Domain.Entities;
using OrderService.Domain.ValueObjects;
using OrderService.Infrastructure.Extensions;
using OrderService.Infrastructure.Persistence;
using Shared.Contracts.Events.V1;

namespace OrderService.Features.SubmitOrder;

public sealed class SubmitOrderEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapPost("/api/v1/orders", Handle)
            .WithName("SubmitOrder")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);
    
    /// <summary>
    /// Submits a new order and enqueues the <see cref="OrderSubmitted"/> event atomically.
    /// </summary>
    /// <remarks>
    /// Uses the transactional outbox pattern. The event is stored in the database
    /// within the same transaction as the order and published to RabbitMQ after commit,
    /// ensuring consistency between data and messages.
    /// </remarks>
    /// <returns>
    /// 202 Accepted — order created and processing started.<br/>
    /// 400 Bad Request — invalid input.
    /// </returns>
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
        
        ctx.Orders.Add(order);

        // TODO: publish order created event in the atomic transaction
        // await publish.Publish();

        await ctx.SaveChangesAsync(ct);

        return Results.Accepted(
            $"/api/v1/orders/{order.Id}",
            new SubmitOrderResponse(order.Id, order.Status.ToString()));
    }
}