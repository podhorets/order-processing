using System.Diagnostics.Metrics;

namespace PaymentService.Infrastructure.Observability;

public sealed class PaymentMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _processed;

    public PaymentMetrics(IMeterFactory meterFactory)
    {
        _meter     = meterFactory.Create("PaymentService");
        _processed = _meter.CreateCounter<long>("payments.processed", description: "Total payments processed");
    }

    public void PaymentProcessed() => _processed.Add(1);

    public void Dispose() => _meter.Dispose();
}
