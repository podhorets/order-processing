using InventoryService.Infrastructure.Http;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Features.GetInventory;

public sealed class GetInventoryEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapGet("/api/v1/inventories/{sku}", Handle)
            .WithName("GetInventory")
            .WithTags("Inventories")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(string sku, InventoryDbContext ctx, CancellationToken ct)
    {
        var inv = await ctx.Inventories
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Sku == sku, ct);

        return inv is null
            ? Results.NotFound()
            : Results.Ok(new { inv.Id, inv.Sku, inv.OnHand, inv.Reserved, inv.Available });
    }
}
