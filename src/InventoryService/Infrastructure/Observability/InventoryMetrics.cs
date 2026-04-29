using System.Diagnostics.Metrics;

namespace InventoryService.Infrastructure.Observability;

public sealed class InventoryMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _reservationsSucceeded;
    private readonly Counter<long> _reservationsFailed;

    public InventoryMetrics(IMeterFactory meterFactory)
    {
        _meter                 = meterFactory.Create("InventoryService");
        _reservationsSucceeded = _meter.CreateCounter<long>("inventory.reservations.succeeded", description: "Total successful reservations");
        _reservationsFailed    = _meter.CreateCounter<long>("inventory.reservations.failed",    description: "Total failed reservations");
    }

    public void ReservationSucceeded() => _reservationsSucceeded.Add(1);
    public void ReservationFailed()    => _reservationsFailed.Add(1);

    public void Dispose() => _meter.Dispose();
}
