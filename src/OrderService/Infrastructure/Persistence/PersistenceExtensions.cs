using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Messaging.Outbox;

namespace OrderService.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<OrderDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(config.GetConnectionString("Database"));
            opts.AddInterceptors(sp.GetRequiredService<OutboxSignalingInterceptor>());
        });
        return services;
    }

    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Database.Migrate();
    }
}
