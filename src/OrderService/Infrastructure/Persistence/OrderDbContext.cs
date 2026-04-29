using Microsoft.EntityFrameworkCore;
using OrderService.Saga;

namespace OrderService.Infrastructure.Persistence;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    // Wolverine uses this DbSet to load/save saga state automatically.
    public DbSet<OrderSaga> OrderSagas => Set<OrderSaga>();

    // Read model — persists after the saga row is deleted on MarkCompleted().
    public DbSet<OrderSummary> OrderSummaries => Set<OrderSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
}
