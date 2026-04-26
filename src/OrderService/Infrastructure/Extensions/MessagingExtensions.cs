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
        
        return services;
    }
}