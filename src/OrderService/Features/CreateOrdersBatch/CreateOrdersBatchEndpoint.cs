using Contracts.Commands;
using Contracts.Dto;
using OrderService.Infrastructure.Http;
using OrderService.Saga;
using UUIDNext;
using Wolverine;

namespace OrderService.Features.CreateOrdersBatch;

public sealed class CreateOrdersBatchEndpoint : IEndpoint
{
    private const int BatchSize = 500;

    public void Map(IEndpointRouteBuilder app)
        => app.MapPost("/api/v1/orders/batch", Handle)
            .WithName("CreateOrdersBatch")
            .WithTags("Orders")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

    private static async Task<IResult> Handle(
        CreateOrdersBatchRequest request,
        IServiceScopeFactory scopeFactory,
        ILogger<CreateOrdersBatchEndpoint> logger,
        CancellationToken ct)
    {
        if (request.OrderAmount is <= 0 or > 1000)
            return Results.BadRequest("OrderAmount must be between 1 and 1000.");

        var batchId = Uuid.NewSequential();
        var opts    = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct };

        // Build order specs upfront so they can be chunked across parallel workers.
        var orders = Enumerable.Range(0, request.OrderAmount)
            .Select(_ => new
            {
                OrderId    = Uuid.NewSequential(),
                CustomerId = Uuid.NewSequential(),
                Sku        = $"B-{Uuid.NewSequential():N}"
            })
            .ToList();

        await Parallel.ForEachAsync(orders.Chunk(BatchSize), opts, async (chunk, token) =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            foreach (var o in chunk)
            {
                // AddInventoryCommand arrives at inventory-service queue BEFORE
                // ReserveInventoryCommand (emitted by OrderSaga.Start from SubmitOrderCommand).
                // Same queue = FIFO → inventory is guaranteed to exist at reservation time.
                await bus.SendAsync(new AddInventoryCommand(o.Sku, 1));
                await bus.SendAsync(new SubmitOrderCommand(
                    o.OrderId,
                    o.CustomerId,
                    9.99m,
                    new[] { new OrderItemDto(o.Sku, 1, 9.99m) }));
            }
        });

        logger.LogInformation("Batch {BatchId}: {Count} orders queued", batchId, request.OrderAmount);

        return Results.Accepted(null, new CreateOrdersBatchResponse(batchId, request.OrderAmount));
    }
}

public sealed record CreateOrdersBatchRequest(int OrderAmount);
public sealed record CreateOrdersBatchResponse(Guid BatchId, int OrdersQueued);
