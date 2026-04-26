using OrderService.Features.FulfillOrder;
using OrderService.Features.InitiatePayment;
using OrderService.Features.ProcessPayment;
using OrderService.Features.RejectOrder;
using OrderService.Features.ReserveInventory;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
        {
            Uri = new Uri(config.GetConnectionString("RabbitMq")!)
        });

        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<IMessageDispatcher, MessageDispatcher>();

        services.AddHostedService<RabbitMqInitializer>();
        services.AddHostedService<RabbitMqConsumerService>();

        services.AddScoped<ReserveInventoryHandler>();
        services.AddScoped<InitiatePaymentHandler>();
        services.AddScoped<ProcessPaymentHandler>();
        services.AddScoped<FulfillOrderHandler>();
        services.AddScoped<RejectOrderHandler>();

        return services;
    }
}
