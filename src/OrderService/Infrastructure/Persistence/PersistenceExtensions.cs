using Microsoft.EntityFrameworkCore;

namespace OrderService.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<OrderDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Database")));
        return services;
    }

    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Database.Migrate();
    }
}
