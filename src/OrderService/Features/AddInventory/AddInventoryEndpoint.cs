using OrderService.Domain.Entities;
using OrderService.Infrastructure.Http;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Features.AddInventory;

public sealed class AddInventoryEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapPost("/api/v1/inventories", Handle)
            .WithName("AddInventory")
            .WithTags("Inventories")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

    private static async Task<IResult> Handle(
        AddInventoryRequest request,
        OrderDbContext ctx,
        CancellationToken ct)
    {
        var inventory = new Inventory(request.Sku, request.OnHand);
        ctx.Inventories.Add(inventory);
        await ctx.SaveChangesAsync(ct);
        return Results.Created($"/inventories/{inventory.Id}", new { inventory.Id });
    }
}

public sealed record AddInventoryRequest(string Sku, int OnHand);
