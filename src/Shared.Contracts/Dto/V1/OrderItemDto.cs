namespace Shared.Contracts.Dto.V1;

public sealed record OrderItemDto(string Sku, int Quantity, decimal UnitPrice);