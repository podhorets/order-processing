using Contracts.Commands;
using InventoryService.Infrastructure.Http;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Features.AddInventory;

public sealed class AddInventoryEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapPost("/api/v1/inventories", Handle)
            .WithName("AddInventory")
            .WithTags("Inventories")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

    private static async Task<IResult> Handle(
        AddInventoryRequest request,
        InventoryDbContext ctx,
        ILogger<AddInventoryEndpoint> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
            return Results.BadRequest("SKU must not be empty.");
        if (request.OnHand <= 0)
            return Results.BadRequest("OnHand must be greater than zero.");

        await AddInventoryHandler.Handle(new AddInventoryCommand(request.Sku, request.OnHand), ctx, logger, ct);

        var inv = await ctx.Inventories.AsNoTracking().FirstAsync(i => i.Sku == request.Sku, ct);
        return Results.Ok(new { inv.Id, inv.Sku, inv.OnHand, inv.Available });
    }
}

public sealed record AddInventoryRequest(string Sku, int OnHand);
