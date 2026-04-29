using JasperFx.Core;
using Wolverine.Attributes;

namespace OrderService.Saga;

// Wolverine schedules this automatically when returned from the saga Start method.
// TimeoutMessage carries the scheduling delay as a built-in property.
[MessageIdentity("order-saga-timeout")]
public record OrderSagaTimeout(Guid OrderId) : Wolverine.TimeoutMessage(5.Minutes());
