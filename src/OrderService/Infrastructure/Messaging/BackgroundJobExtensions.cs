using OrderService.Infrastructure.Messaging.Outbox;

namespace OrderService.Infrastructure.Messaging;

public static class OutboxExtensions
{
    public static IServiceCollection AddOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxSettings>(configuration.GetSection("Outbox"));

        services.AddSingleton<OutboxChannel>();
        services.AddSingleton<OutboxSignalingInterceptor>();
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxProcessorService>();

        return services;
    }
}
