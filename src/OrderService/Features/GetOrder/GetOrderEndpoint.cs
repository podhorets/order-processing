using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Http;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Features.GetOrder;

public sealed class GetOrderEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapGet("/api/v1/orders/{id:guid}", Handle)
            .WithName("GetOrder")
            .WithTags("Orders")
            .Produces<GetOrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(Guid id, OrderDbContext ctx, CancellationToken ct)
    {
        var summary = await ctx.OrderSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return summary is null
            ? Results.NotFound()
            : Results.Ok(new GetOrderResponse(
                summary.Id,
                summary.CustomerId,
                summary.Status,
                summary.TotalAmount,
                summary.RejectionReason,
                summary.CreatedAt,
                summary.CompletedAt));
    }
}
