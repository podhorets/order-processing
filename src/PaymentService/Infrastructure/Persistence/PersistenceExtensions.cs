using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace PaymentService.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContextWithWolverineIntegration<PaymentDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Database")));

        return services;
    }

    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.Migrate();
    }
}
