namespace Contracts.Commands;

/// <summary>
/// Published by OrderService batch endpoints → InventoryService creates/top-ups stock.
/// Inserted before the corresponding ReserveInventoryCommand in the same queue — FIFO guarantees
/// inventory exists before reservation is attempted.
/// </summary>
public record AddInventoryCommand(string Sku, int OnHand);
