using System.Diagnostics.Metrics;

namespace OrderService.Infrastructure.Observability;

public sealed class OrderMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _submitted;
    private readonly Counter<long> _completed;
    private readonly Counter<long> _rejected;
    private readonly Counter<long> _timedOut;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        _meter     = meterFactory.Create("OrderService");
        _submitted = _meter.CreateCounter<long>("orders.submitted", unit: "orders", description: "Total orders submitted");
        _completed = _meter.CreateCounter<long>("orders.completed", unit: "orders", description: "Total orders successfully fulfilled");
        _rejected  = _meter.CreateCounter<long>("orders.rejected",  unit: "orders", description: "Total orders rejected");
        _timedOut  = _meter.CreateCounter<long>("orders.timed_out", unit: "orders", description: "Total orders timed out");
    }

    public void OrderSubmitted() => _submitted.Add(1);
    public void OrderCompleted() => _completed.Add(1);
    public void OrderRejected()  => _rejected.Add(1);
    public void OrderTimedOut()  => _timedOut.Add(1);

    public void Dispose() => _meter.Dispose();
}
