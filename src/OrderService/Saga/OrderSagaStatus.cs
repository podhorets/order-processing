namespace OrderService.Saga;

public enum OrderSagaStatus
{
    ReservingInventory,
    ProcessingPayment,
    Fulfilling,
    Compensating,
    Completed,
    Failed,
    TimedOut
}
