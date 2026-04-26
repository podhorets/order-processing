using OrderService.Infrastructure.Messaging;
using ReleaseInventoryCommand = Shared.Contracts.Commands.V1.ReleaseInventory;

namespace OrderService.Features.ReleaseInventory;

public sealed class ReleaseInventoryHandler : IMessageHandler<ReleaseInventoryCommand>
{
    public Task HandleAsync(ReleaseInventoryCommand message, CancellationToken ct)
    {
        // TODO: release inventory
        return Task.CompletedTask;
    }
}