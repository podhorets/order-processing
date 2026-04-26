using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Common;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Messaging.Outbox;

namespace OrderService.Infrastructure.Persistence;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreatedAt(now);
                    break;
                case EntityState.Modified:
                    entry.Entity.SetUpdatedAt(now);
                    break;
            }
        }
        
        return await base.SaveChangesAsync(ct);
    }
}
