using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Http;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Features.GetOrder;

public sealed class GetOrderEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapGet("/api/v1/orders/{id:guid}", Handle)
            .WithName("GetOrder")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(
        Guid id,
        OrderDbContext ctx,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Results.BadRequest("OrderId must not be empty.");

        var order = await ctx.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return order is null
            ? Results.NotFound()
            : Results.Ok(new GetOrderResponse(order.Id, order.CustomerId, order.Status.ToString(), order.TotalAmount));
    }
}
