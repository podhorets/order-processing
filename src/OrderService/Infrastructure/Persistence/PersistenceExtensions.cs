using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace OrderService.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        // AddDbContextWithWolverineIntegration wires the DbContext into Wolverine's
        // transactional middleware — outbox messages are written atomically with SaveChangesAsync.
        services.AddDbContextWithWolverineIntegration<OrderDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Database")));

        return services;
    }

    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.Migrate();
    }
}
