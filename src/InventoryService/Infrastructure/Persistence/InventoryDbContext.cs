using InventoryService.Domain.Common;
using InventoryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Persistence;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

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
