using Contracts.Dto;
using FluentValidation;
using OrderService.Infrastructure.Http;
using OrderService.Infrastructure.Observability;
using OrderService.Saga;
using UUIDNext;
using Wolverine;

namespace OrderService.Features.SubmitOrder;

public sealed class SubmitOrderEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapPost("/api/v1/orders", Handle)
            .WithName("SubmitOrder")
            .WithTags("Orders")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

    private static async Task<IResult> Handle(
        SubmitOrderRequest request,
        IValidator<SubmitOrderRequest> validator,
        IMessageBus bus,
        OrderMetrics metrics,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var orderId     = Uuid.NewSequential();
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var items       = request.Items.Select(i => new OrderItemDto(i.Sku, i.Quantity, i.UnitPrice)).ToList();

        await bus.SendAsync(new SubmitOrderCommand(orderId, request.CustomerId, totalAmount, items));

        metrics.OrderSubmitted();

        return Results.Accepted($"/api/v1/orders/{orderId}", new { OrderId = orderId });
    }
}
