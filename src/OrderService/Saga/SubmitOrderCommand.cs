using Contracts.Dto;

namespace OrderService.Saga;

/// <summary>
/// Internal command — never leaves OrderService via RabbitMQ.
/// Published via IMessageBus from the HTTP endpoint; handled locally by OrderSaga.Start.
/// </summary>
public record SubmitOrderCommand(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items);
