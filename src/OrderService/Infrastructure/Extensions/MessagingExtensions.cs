using OrderService.Features.ProcessOrder;
using OrderService.Features.RejectOrder;
using OrderService.Features.ReserveInventory;
using OrderService.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Extensions;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            Uri = new Uri(config.GetConnectionString("RabbitMq")!)
        });

        services.AddSingleton<RabbitMqPublisher>();
        services.AddHostedService<RabbitMqInitializer>();
        
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<IMessageDispatcher, MessageDispatcher>();

        services.AddScoped<OrderSubmittedHandler>();
        services.AddScoped<InventoryReservedHandler>();
        services.AddScoped<InventoryReservationFailedHandler>();

        services.AddHostedService<RabbitMqConsumerService>();

        return services;
    }
}