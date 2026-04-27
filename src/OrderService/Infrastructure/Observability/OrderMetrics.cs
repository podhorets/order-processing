using System.Diagnostics.Metrics;

namespace OrderService.Infrastructure.Observability;

public sealed class OrderMetrics(IMeterFactory meterFactory)
{
    private readonly Counter<long> _processed = meterFactory
        .Create("OrderService")
        .CreateCounter<long>("orders.processed", unit: "orders", description: "Total fulfilled orders");

    public void OrderProcessed() => _processed.Add(1);
}
