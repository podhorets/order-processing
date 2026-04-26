using Microsoft.Extensions.Options;

namespace OrderService.Infrastructure.Messaging.Outbox;

public sealed class OutboxProcessorService(
    OutboxChannel channel,
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxSettings> settings,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(settings.Value.FallbackPollInterval);

                try
                {
                    await channel.WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // fallback poll interval elapsed - process anyway
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                await processor.ProcessBatchAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in outbox processor loop");
            }
        }
    }
}
